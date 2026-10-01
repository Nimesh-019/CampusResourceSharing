using CampusResourceSharing.Models;
using System.ComponentModel.DataAnnotations;

namespace CampusResourceSharing.ViewModels
{
    public class ItemReviewsViewModel
    {
        public Item Item { get; set; } = null!;
        public List<Review> Reviews { get; set; } = new();

        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }

        // Eligibility details for the current user
        public bool IsUserLoggedIn { get; set; }
        public bool IsOwner { get; set; }
        public bool IsEligibleToReview { get; set; }
        public int? EligibleBorrowRequestId { get; set; }
        public bool HasActiveBorrowing { get; set; }
        public DateTime? ActiveBorrowingEndDate { get; set; }
        public bool HasAlreadyReviewedAll { get; set; }
        public string? EligibilityMessage { get; set; }

        // New review form data
        public CreateReviewViewModel NewReview { get; set; } = new();
    }

    public class CreateReviewViewModel
    {
        [Required]
        public int ItemId { get; set; }

        [Required]
        public int BorrowRequestId { get; set; }

        [Required(ErrorMessage = "Please select a rating between 1 and 5 stars.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
        public int Rating { get; set; } = 5;

        [Required(ErrorMessage = "Review comment is required.")]
        [StringLength(1000, MinimumLength = 3, ErrorMessage = "Review comment must be between 3 and 1000 characters.")]
        public string Comment { get; set; } = string.Empty;
    }
}
