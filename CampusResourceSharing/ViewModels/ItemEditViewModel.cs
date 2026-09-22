using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CampusResourceSharing.ViewModels
{
    public class ItemEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Condition { get; set; }

        public IFormFile? Image { get; set; }

        public bool IsAvailable { get; set; } = true;

        public string? ExistingImagePath { get; set; }
    }
}