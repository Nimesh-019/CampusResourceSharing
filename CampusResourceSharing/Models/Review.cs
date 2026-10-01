using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CampusResourceSharing.Models
{
    public class Review
    {
        public int Id { get; set; }

        // Item being reviewed
        [Required]
        public int ItemId { get; set; }

        [ValidateNever]
        public Item? Item { get; set; }

        // Student who wrote the review
        [Required]
        [ValidateNever]
        public string ReviewerId { get; set; } = string.Empty;

        [ValidateNever]
        public ApplicationUser? Reviewer { get; set; }

        // Completed borrow request tied to this review
        [Required]
        public int BorrowRequestId { get; set; }

        [ValidateNever]
        public Request? BorrowRequest { get; set; }

        [Required(ErrorMessage = "Rating is required.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Review comment is required.")]
        [StringLength(1000, MinimumLength = 3, ErrorMessage = "Review comment must be between 3 and 1000 characters.")]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}