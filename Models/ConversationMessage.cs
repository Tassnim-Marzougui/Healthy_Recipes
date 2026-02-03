using System;

namespace Healthy_Recipes.Models
{
    public class ConversationMessage
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public Conversation? Conversation { get; set; }
        public string Role { get; set; } = string.Empty; // system | user | assistant
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
