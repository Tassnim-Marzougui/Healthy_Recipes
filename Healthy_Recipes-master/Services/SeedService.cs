using Microsoft.AspNetCore.Identity;
using Healthy_Recipes.Data;
using Healthy_Recipes.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Healthy_Recipes.Services
{
    public static class SeedService
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Créer les rôles
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }

            // Créer l'utilisateur admin
            var adminEmail = "admin@healthyrecipes.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new Users
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Administrateur Système"
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Créer un utilisateur normal
            var userEmail = "user@healthyrecipes.com";
            var normalUser = await userManager.FindByEmailAsync(userEmail);
            if (normalUser == null)
            {
                normalUser = new Users
                {
                    UserName = userEmail,
                    Email = userEmail,
                    FullName = "Utilisateur Test"
                };
                var result = await userManager.CreateAsync(normalUser, "User@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(normalUser, "User");
                }
            }

            // Ajouter des recettes de test
            if (!context.Recipes.Any())
            {
                context.Recipes.AddRange(
                    new Recipe
                    {
                        Title = "Salade de quinoa aux légumes",
                        Description = "Une salade riche en protéines et pleine de saveurs avec des légumes de saison.",
                        Category = "Végétarien",
                        PreparationTime = 20,
                        ImageUrl = "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?ixlib=rb-4.0.3&auto=format&fit=crop&w=880&q=80",
                        Ingredients = "1 tasse de quinoa cuit\n1 concombre\n2 tomates\n1 avocat\n1/4 tasse d'olives\n2 c. à soupe d'huile d'olive\nJus de citron\nSel et poivre",
                        Instructions = "Rincer le quinoa à l'eau froide, puis le faire cuire selon les instructions sur l'emballage.\nPendant que le quinoa cuit, couper les légumes en dés de taille similaire.\nDans un grand bol, mélanger le quinoa cuit et refroidi avec les légumes.\nArroser d'huile d'olive et de jus de citron, puis assaisonner avec du sel et du poivre.\nMélanger délicatement et servir immédiatement ou réfrigérer jusqu'au moment de servir.",
                        Calories = 320,
                        Protein = 12,
                        Carbohydrates = 35,
                        Fat = 15
                    },
                    new Recipe
                    {
                        Title = "Bowl protéiné au saumon",
                        Description = "Un bowl équilibré avec du saumon grillé, avocat, quinoa et légumes croquants.",
                        Category = "Poisson",
                        PreparationTime = 25,
                        ImageUrl = "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?ixlib=rb-4.0.3&auto=format&fit=crop&w=880&q=80",
                        Ingredients = "150g de saumon frais\n1/2 tasse de quinoa cuit\n1/2 avocat\n1/2 concombre\n1 carotte\n1 c. à soupe de graines de sésame\n1 c. à soupe de sauce soja\n1 c. à soupe d'huile d'olive",
                        Instructions = "Faire cuire le quinoa selon les instructions.\nAssaisonner le saumon avec du sel et du poivre, puis le faire griller à la poêle avec un peu d'huile d'olive.\nCouper les légumes en fines lamelles ou en dés.\nDans un bol, disposer le quinoa, les légumes et le saumon.\nSaupoudrer de graines de sésame et arroser de sauce soja.",
                        Calories = 450,
                        Protein = 30,
                        Carbohydrates = 40,
                        Fat = 20
                    },
                    new Recipe
                    {
                        Title = "Smoothie vert énergisant",
                        Description = "Un smoothie revitalisant à base d'épinards, banane, pomme et graines de chia.",
                        Category = "Vegan",
                        PreparationTime = 5,
                        ImageUrl = "https://images.unsplash.com/photo-1490645935967-10de6ba17061?ixlib=rb-4.0.3&auto=format&fit=crop&w=880&q=80",
                        Ingredients = "1 poignée d'épinards frais\n1 banane\n1/2 pomme\n1 c. à café de graines de chia\n200ml de lait d'amande\n1 c. à café de miel (optionnel)",
                        Instructions = "Laver les épinards et les mettre dans le blender.\nAjouter la banane coupée en morceaux et la pomme.\nVerser le lait d'amande et les graines de chia.\nMixer jusqu'à obtenir une texture lisse.\nGoûter et ajouter du miel si besoin de plus de douceur.",
                        Calories = 200,
                        Protein = 5,
                        Carbohydrates = 30,
                        Fat = 8
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}