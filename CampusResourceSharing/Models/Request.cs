using System.ComponentModel.DataAnnotations;
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
        public IdentityUser? Requester { get; set; }

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
    }
}