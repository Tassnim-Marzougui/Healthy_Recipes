using System;
using System.Collections.Generic;

namespace Healthy_Recipes.Models
{
    public class Conversation
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public List<ConversationMessage> Messages { get; set; } = new();
    }
}
