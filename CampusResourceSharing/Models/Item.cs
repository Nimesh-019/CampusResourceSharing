using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CampusResourceSharing.Models
{
    public class Item
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

        [ValidateNever]
        public string OwnerId { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Condition { get; set; }

        [StringLength(500)]
        public string? ImagePath { get; set; }

        public bool IsAvailable { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}