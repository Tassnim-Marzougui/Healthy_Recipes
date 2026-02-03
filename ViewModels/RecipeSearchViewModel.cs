using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Healthy_Recipes.ViewModels
{
    public class RecipeSearchViewModel
    {
        [Display(Name = "Rechercher")]
        [Required(ErrorMessage = "Le terme de recherche est requis")]
        public string SearchTerm { get; set; } = string.Empty;

        [Display(Name = "Catégorie")]
        public string Category { get; set; } = string.Empty;

        public List<string> Categories { get; set; } = new List<string>
        {
            "Toutes les catégories",
            "Végétarien",
            "Vegan",
            "Sans gluten",
            "Protéiné",
            "Faible en calories"
        };

        public List<RecipeViewModel> Recipes { get; set; } = new List<RecipeViewModel>();
    }
}
