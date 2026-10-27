using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CampusResourceSharing.Models
{
    public class Request
    {
        public int Id { get; set; }

        // Item being requested
        [Required]
        public int ItemId { get; set; }

        [ValidateNever]
        public Item? Item { get; set; }

        // User who is requesting the item
        [Required]
        [ValidateNever]
        public string RequesterId { get; set; } = string.Empty;

        [ValidateNever]
        public ApplicationUser? Requester { get; set; }

        // Optional message from requester
        [StringLength(500)]
        public string? Message { get; set; }

        // Pending / Accepted / Rejected
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        // Duration of request
        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today;

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        [Display(Name = "Responded At")]
        public DateTime? RespondedAt { get; set; }

        [ValidateNever]
        public Review? Review { get; set; }
        // ==========================================
        // Dynamic Borrowing Status Calculation
        // ==========================================

        
        [NotMapped]
        public string? BorrowingStatus => GetBorrowingStatus(DateTime.Today);


        public string? GetBorrowingStatus(DateTime asOfDate)
        {
            if (!string.Equals(Status, "Accepted", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var checkDate = asOfDate.Date;
            if (checkDate < StartDate.Date)
            {
                return "Upcoming";
            }
            if (checkDate <= EndDate.Date)
            {
                return "Ongoing";
            }
            return "Completed";
        }

        [NotMapped]
        public bool IsUpcoming => BorrowingStatus == "Upcoming";

        [NotMapped]
        public bool IsOngoing => BorrowingStatus == "Ongoing";

        [NotMapped]
        public bool IsCompleted => BorrowingStatus == "Completed";

        [NotMapped]
        public string DisplayStatus => BorrowingStatus ?? Status;
    }
}