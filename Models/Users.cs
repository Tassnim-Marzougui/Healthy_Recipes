using Microsoft.AspNetCore.Identity;

namespace Healthy_Recipes.Models
{
    public class Users : IdentityUser
    {
        public required string FullName { get; set; }
    }
}