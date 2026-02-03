using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Healthy_Recipes.Data;
using Healthy_Recipes.Models;

namespace Healthy_Recipes.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public ChatController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ChatController> logger, ApplicationDbContext db)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _db = db;
        }

        public class ChatRequest { [JsonPropertyName("message")] public string? Message { get; set; } [JsonPropertyName("contextId")] public string? ContextId { get; set; } [JsonPropertyName("weekly")] public bool Weekly { get; set; } }
        public class ChatResponse { public string? reply { get; set; } public object? raw { get; set; } }

        // In-memory context store
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, UserContext> _contexts = new();

        public class UserContext
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string? Goal { get; set; }
            public List<string> Allergies { get; set; } = new();
            public int? TimeAvailableMinutes { get; set; }
            public string? CookingLevel { get; set; }
            public string? Budget { get; set; }
        }

        public class MealItem { public int Id { get; set; } public string Title { get; set; } = string.Empty; public int Calories { get; set; } }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message is required." });

            // Resolve or create conversation
            Conversation? conversation = null;
            if (!string.IsNullOrEmpty(request.ContextId) && int.TryParse(request.ContextId, out var providedId))
            {
                conversation = await _db.Conversations.Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == providedId, cancellationToken);
            }

            if (conversation == null)
            {
                var session = HttpContext.Session;
                var sessionConv = session.GetString("ConversationId");
                if (!string.IsNullOrEmpty(sessionConv) && int.TryParse(sessionConv, out var scid))
                {
                    conversation = await _db.Conversations.Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == scid, cancellationToken);
                }
            }

            if (conversation == null)
            {
                conversation = new Conversation { SessionId = HttpContext.Session.Id };
                _db.Conversations.Add(conversation);
                await _db.SaveChangesAsync(cancellationToken);
                HttpContext.Session.SetString("ConversationId", conversation.Id.ToString());
            }

            // Persist user message
            var userMessage = new ConversationMessage { ConversationId = conversation.Id, Role = "user", Content = request.Message };
            _db.ConversationMessages.Add(userMessage);
            await _db.SaveChangesAsync(cancellationToken);

            // Re-load messages to include the saved one
            await _db.Entry(conversation).Collection(c => c.Messages).LoadAsync(cancellationToken);

            // Build profile from conversation messages
            var profile = ExtractProfileFromConversation(conversation);

            // Build system prompt (French + Tunisian dialect), include preferences
            var systemPrompt = BuildSystemPrompt(profile);

            // Build messages for API: system + last N messages
            var messagesForApi = new List<object> { new { role = "system", content = systemPrompt } };
            var lastMessages = conversation.Messages.OrderBy(m => m.CreatedAt).Select(m => new { role = m.Role == "assistant" ? "assistant" : "user", content = m.Content }).ToList();
            messagesForApi.AddRange(lastMessages.TakeLast(20));

            // Add current user message (already saved) to ensure it's present
            messagesForApi.Add(new { role = "user", content = request.Message });

            // Call LLM (Groq)
            var apiKey = _configuration["Groq:ApiKey"] ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
            var baseUrl = _configuration["Groq:BaseUrl"] ?? Environment.GetEnvironmentVariable("GROQ_BASE_URL") ?? "https://api.groq.com/openai/v1/chat/completions";
            var modelName = _configuration["Groq:Model"] ?? Environment.GetEnvironmentVariable("GROQ_MODEL") ?? "llama-3.3-70b-versatile";

            if (string.IsNullOrEmpty(apiKey))
                return Problem(detail: "Groq API key not configured.", statusCode: 500);

            var payload = new { model = modelName, messages = messagesForApi, temperature = 0.2, max_tokens = 1024 };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            string llmReply;
            string rawBody;
            try
            {
                var resp = await client.PostAsync(baseUrl, httpContent, cancellationToken);
                rawBody = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("LLM error {Status}: {Body}", (int)resp.StatusCode, rawBody);
                    return StatusCode((int)resp.StatusCode, new { error = "LLM error", detail = rawBody });
                }

                llmReply = TryExtractReply(rawBody, out var parsed) ?? rawBody;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error calling LLM");
                return StatusCode(502, new { error = "Erreur réseau ou LLM", detail = ex.Message });
            }

            // Persist assistant message
            var assistantMessage = new ConversationMessage { ConversationId = conversation.Id, Role = "assistant", Content = llmReply };
            _db.ConversationMessages.Add(assistantMessage);
            conversation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            // Provide recipe suggestions (up to 3) based on extracted profile
            var suggestions = await SuggestRecipesForProfile(profile, 3, cancellationToken);

            // If weekly requested, build week of simple plans
            if (request.Weekly)
            {
                var days = new List<object>();
                var rnd = new Random();
                for (int i = 0; i < 7; i++)
                {
                    var plan = await BuildDailyPlan(profile, GetDailyCaloriesForGoal(profile.Goal), cancellationToken, rnd);
                    days.Add(plan);
                }

                return Ok(new { reply = llmReply, contextId = conversation.Id, suggestions = suggestions, weekly = true, days = days });
            }

            return Ok(new { reply = llmReply, contextId = conversation.Id, suggestions = suggestions });
        }

        private static string? DetectGoal(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return null;
            var m = message.ToLowerInvariant();
            if (m.Contains("perdre") || m.Contains("maigrir") || m.Contains("perte de poids")) return "weight_loss";
            if (m.Contains("muscle") || m.Contains("prise de masse") || m.Contains("prendre du muscle")) return "muscle_gain";
            return "healthy";
        }

        private static void ExtractContextFromMessage(string message, UserContext ctx)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            var m = message.ToLowerInvariant();

            // Allergies: look for 'sans' or 'allergie' followed by words
            if (m.Contains("sans ") || m.Contains("allergie") || m.Contains("intolérant") )
            {
                var allergens = new List<string> { "gluten", "lactose", "lait", "noix", "arachide", "oeuf", "soja", "poisson", "crustacé" };
                foreach (var a in allergens)
                {
                    if (m.Contains(a))
                    {
                        if (!ctx.Allergies.Contains(a)) ctx.Allergies.Add(a);
                    }
                }
            }

            // Time available
            var timeMatch = System.Text.RegularExpressions.Regex.Match(m, "(\\d{1,3})\\s*(minutes|min|mn|heure|heures|h)");
            if (timeMatch.Success)
            {
                if (int.TryParse(timeMatch.Groups[1].Value, out var t))
                {
                    // if hours detected, convert to minutes
                    if (m.Contains("heure") || m.Contains("h")) t *= 60;
                    ctx.TimeAvailableMinutes = t;
                }
            }

            // Cooking level
            if (m.Contains("débutant") || m.Contains("facile") || m.Contains("simple")) ctx.CookingLevel = "beginner";
            if (m.Contains("moyen") || m.Contains("intermédiaire")) ctx.CookingLevel = "intermediate";
            if (m.Contains("avancé") || m.Contains("difficile") || m.Contains("expert")) ctx.CookingLevel = "advanced";

            // Budget
            if (m.Contains("pas cher") || m.Contains("bon marché") || m.Contains("économique") || m.Contains("cheap")) ctx.Budget = "low";
            if (m.Contains("moyen") || m.Contains("normal")) ctx.Budget = "medium";
            if (m.Contains("cher") || m.Contains("lux")) ctx.Budget = "high";
        }

        private static List<string> GetMissingFields(UserContext ctx)
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(ctx.Goal)) missing.Add("goal");
            if (!ctx.Allergies.Any()) missing.Add("allergies");
            if (!ctx.TimeAvailableMinutes.HasValue) missing.Add("time");
            if (string.IsNullOrEmpty(ctx.CookingLevel)) missing.Add("cooking_level");
            if (string.IsNullOrEmpty(ctx.Budget)) missing.Add("budget");
            return missing;
        }

        private static string AskForNextField(string field)
        {
            return field switch
            {
                "goal" => "Quel est ton objectif ? (perdre du poids / prendre du muscle / mode de vie sain)",
                "allergies" => "As-tu des allergies ou aliments à éviter ? (par ex. sans gluten, sans lait)",
                "time" => "Combien de minutes as-tu pour préparer chaque plat en général ?",
                "cooking_level" => "Ton niveau en cuisine ? (débutant / moyen / avancé)",
                "budget" => "Quel est ton budget ? (pas cher / moyen / élevé)",
                _ => "Peux-tu préciser s'il te plaît ?"
            };
        }

        private static int GetDailyCaloriesForGoal(string? goal)
        {
            return goal switch
            {
                "weight_loss" => 1800,
                "muscle_gain" => 2500,
                _ => 2200,
            };
        }

        private async Task<dynamic> BuildDailyPlan(UserContext ctx, int dailyTarget, CancellationToken cancellationToken, Random rnd)
        {
            // Targets per meal
            var breakfastTarget = (int)(dailyTarget * 0.25);
            var lunchTarget = (int)(dailyTarget * 0.40);
            var dinnerTarget = dailyTarget - breakfastTarget - lunchTarget;

            // Helper to pick recipe near target
            MealItem PickClosest(IEnumerable<Recipe> candidates, int target)
            {
                var best = candidates
                    .Select(r => new MealItem { Id = r.Id, Title = r.Title, Calories = r.Calories })
                    .OrderBy(r => Math.Abs(r.Calories - target))
                    .FirstOrDefault();
                return best ?? new MealItem { Id = 0, Title = "Aucune recette trouvée", Calories = 0 };
            }

            // Build base query: filter by allergies and by max prep time if provided
            IQueryable<Recipe> q = _db.Recipes;
            if (ctx.Allergies.Any())
            {
                foreach (var a in ctx.Allergies)
                {
                    var aLower = a.ToLowerInvariant();
                    q = q.Where(r => !r.Ingredients.ToLower().Contains(aLower));
                }
            }
            if (ctx.TimeAvailableMinutes.HasValue)
            {
                q = q.Where(r => r.PreparationTime <= ctx.TimeAvailableMinutes.Value);
            }

            var allCandidates = await q.ToListAsync(cancellationToken);
            // If few candidates, relax time constraint
            if (!allCandidates.Any() && ctx.TimeAvailableMinutes.HasValue)
            {
                allCandidates = await _db.Recipes.Where(r => !ctx.Allergies.Any(a => r.Ingredients.ToLower().Contains(a.ToLower()))).ToListAsync(cancellationToken);
            }

            // Select meals
            var breakfastCandidates = allCandidates.Where(r => r.Calories > 0).OrderBy(r => r.Calories).ToList();
            var lunchCandidates = breakfastCandidates;
            var dinnerCandidates = breakfastCandidates;

            var breakfast = PickClosest(breakfastCandidates, breakfastTarget);
            var lunch = PickClosest(lunchCandidates, lunchTarget);
            var dinner = PickClosest(dinnerCandidates, dinnerTarget);

            return new { breakfast, lunch, dinner };
        }

        private UserContext ExtractProfileFromConversation(Conversation conv)
        {
            var ctx = new UserContext();
            foreach (var m in conv.Messages.OrderBy(m => m.CreatedAt))
            {
                if (m.Role == "user")
                {
                    // aggregate info
                    if (string.IsNullOrEmpty(ctx.Goal)) ctx.Goal = DetectGoal(m.Content);
                    ExtractContextFromMessage(m.Content, ctx);
                }
            }
            // defaults if missing
            if (string.IsNullOrEmpty(ctx.Goal)) ctx.Goal = "healthy";
            if (!ctx.TimeAvailableMinutes.HasValue) ctx.TimeAvailableMinutes = 30;
            if (string.IsNullOrEmpty(ctx.CookingLevel)) ctx.CookingLevel = "beginner";
            if (string.IsNullOrEmpty(ctx.Budget)) ctx.Budget = "medium";
            return ctx;
        }

        private string BuildSystemPrompt(UserContext profile)
        {
            var allergies = profile.Allergies.Any() ? string.Join(", ", profile.Allergies) : "aucune";
            var time = profile.TimeAvailableMinutes.HasValue ? $"{profile.TimeAvailableMinutes.Value} minutes" : "~30 minutes";
            var cooking = profile.CookingLevel ?? "débutant";
            var budget = profile.Budget ?? "moyen";

            var prompt = new StringBuilder();
            prompt.AppendLine("Tu es l'assistant du site HealthyRecipes. Réponds toujours en français, avec un ton court et amical, et ajoute parfois des expressions tunisiennes familières.");
            prompt.AppendLine($"Propose des recettes rapides et saines adaptées à l'utilisateur: allergies = {allergies}; temps de préparation ≤ {time}; niveau = {cooking}; budget = {budget}.");
            prompt.AppendLine("Lorsque c'est possible, fournis jusqu'à 3 suggestions de recettes avec titre, calories approximatives et temps de préparation, formatées clairement.");
            prompt.AppendLine("Si l'utilisateur demande un plan hebdomadaire, fournis une réponse JSON structurée avec 7 jours, chaque jour ayant petit-déj/déjeuner/dîner et calories.");
            prompt.AppendLine("Ne deviens pas trop verbeux; garde les réponses concises et utiles.");
            prompt.AppendLine("Si une information manque (allergies, temps, budget ou niveau), pose une question courte et directe pour la récupérer.");

            return prompt.ToString();
        }

        private static string? TryExtractReply(string respBody, out object? parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(respBody)) return null;
            try
            {
                using var doc = JsonDocument.Parse(respBody);
                parsed = JsonSerializer.Deserialize<object>(respBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.ValueKind == JsonValueKind.Object)
                    {
                        if (first.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
                        {
                            if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                                return content.GetString();
                        }
                        if (first.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                            return text.GetString();
                    }
                }

                if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array && output.GetArrayLength() > 0)
                {
                    var firstOutput = output[0];
                    if (firstOutput.TryGetProperty("content", out var content))
                    {
                        if (content.ValueKind == JsonValueKind.Array && content.GetArrayLength() > 0)
                        {
                            var sb = new StringBuilder();
                            foreach (var item in content.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.Object)
                                {
                                    if (item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String) sb.AppendLine(t.GetString());
                                    else if (item.TryGetProperty("content", out var c2) && c2.ValueKind == JsonValueKind.String) sb.AppendLine(c2.GetString());
                                    else sb.AppendLine(item.ToString());
                                }
                                else if (item.ValueKind == JsonValueKind.String) sb.AppendLine(item.GetString());
                            }
                            var result = sb.ToString().Trim();
                            if (!string.IsNullOrEmpty(result)) return result;
                        }
                        else if (content.ValueKind == JsonValueKind.String)
                        {
                            var s = content.GetString();
                            if (!string.IsNullOrEmpty(s)) return s;
                        }
                    }
                }

                var firstString = FindFirstString(root);
                if (firstString != null) return firstString;
            }
            catch { }

            parsed = respBody;
            return respBody;
        }

        private static string? FindFirstString(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        var res = FindFirstString(prop.Value);
                        if (res != null) return res;
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        var res = FindFirstString(item);
                        if (res != null) return res;
                    }
                    break;
            }
            return null;
        }

        private async Task<IEnumerable<object>> SuggestRecipesForProfile(UserContext profile, int max, CancellationToken cancellationToken)
        {
            IQueryable<Recipe> q = _db.Recipes;
            if (profile.Allergies.Any())
            {
                foreach (var a in profile.Allergies)
                {
                    var aLower = a.ToLowerInvariant();
                    q = q.Where(r => !r.Ingredients.ToLower().Contains(aLower));
                }
            }
            if (profile.TimeAvailableMinutes.HasValue)
            {
                q = q.Where(r => r.PreparationTime <= profile.TimeAvailableMinutes.Value);
            }

            var list = await q.OrderBy(r => r.Calories).Take(max * 3).ToListAsync(cancellationToken);
            // pick up to `max` varied items
            var selected = list.Take(max).Select(r => new { id = r.Id, title = r.Title, calories = r.Calories, prep = r.PreparationTime }).ToList();
            return selected;
        }
    }
}
