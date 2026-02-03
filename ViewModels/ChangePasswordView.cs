using System.ComponentModel.DataAnnotations;

namespace Healthy_Recipes.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public required string NewPassword { get; set; }
        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Les nouveaux mots de passe ne correspondent pas.")]
        public required string ConfirmNewPassword { get; set; }
    }
}