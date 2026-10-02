using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CampusResourceSharing.Models
{
    public enum ItemStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public class Item
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Item Name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Condition { get; set; }

        [ValidateNever]
        public string OwnerId { get; set; } = string.Empty;

        [ValidateNever]
        [ForeignKey("OwnerId")]
        public ApplicationUser? Owner { get; set; }

        [StringLength(500)]
        public string? ImagePath { get; set; }

        [StringLength(255)]
        public string? ImagePublicId { get; set; }

        public bool IsAvailable { get; set; } = true;

        public ItemStatus Status { get; set; } = ItemStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever]
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}