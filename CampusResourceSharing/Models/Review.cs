using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CampusResourceSharing.Models
{
    public class Review
    {
        public int Id { get; set; }

        // Item being reviewed
        public int ItemId { get; set; }

        // User who wrote the review
        [ValidateNever]
        public string ReviewerId { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}