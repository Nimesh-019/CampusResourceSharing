using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CampusResourceSharing.ViewModels
{
    public class ItemCreateViewModel
    {
        [Required(ErrorMessage = "Item Name is required.")]
        [StringLength(100, ErrorMessage = "Item Name cannot exceed 100 characters.")]
        [Display(Name = "Item Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please select a Category.")]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Condition { get; set; }

        public IFormFile? Image { get; set; }

        [Display(Name = "Is Available")]
        public bool IsAvailable { get; set; } = true;
    }
}