using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Healthy_Recipes.Models;
using Microsoft.AspNetCore.Identity;

namespace Healthy_Recipes.Data
{
    public class ApplicationDbContext : IdentityDbContext<Users>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Recipe> Recipes { get; set; }
    }
}