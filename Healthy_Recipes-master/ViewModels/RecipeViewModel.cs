using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Healthy_Recipes.ViewModels
{
    public class RecipeViewModel
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public int PreparationTime { get; set; }

        public string? ImageUrl { get; set; }
        public IFormFile? Image { get; set; }

        [Required]
        public string Ingredients { get; set; } = string.Empty;

        [Required]
        public string Instructions { get; set; } = string.Empty;

        public int Calories { get; set; }
        public int Protein { get; set; }
        public int Carbohydrates { get; set; }
        public int Fat { get; set; }
    }
}
