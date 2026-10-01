using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using CampusResourceSharing.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusResourceSharing.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Review/Index?itemId=5 or /Review/Index/5
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(int? id, int? itemId)
        {
            int targetItemId = itemId ?? id ?? 0;
            if (targetItemId <= 0)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var viewModel = await BuildReviewsViewModelAsync(targetItemId, currentUserId);

            if (viewModel == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        // GET: /Review/ItemReviews?itemId=5
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ItemReviews(int itemId)
        {
            return await Index(itemId, itemId);
        }

        // GET: /Review/ReviewsModal?itemId=5
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ReviewsModal(int itemId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var viewModel = await BuildReviewsViewModelAsync(itemId, currentUserId);

            if (viewModel == null)
            {
                return NotFound();
            }

            return PartialView("_ItemReviewsModalPartial", viewModel);
        }

        // POST: /Review/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateReviewViewModel model)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == model.ItemId);

            if (item == null)
            {
                return NotFound();
            }

            // 1. Owner restriction: Owner cannot review their own item
            if (item.OwnerId == currentUserId)
            {
                TempData["Error"] = "As the owner of this item, you cannot review your own item.";
                return RedirectToAction(nameof(Index), new { itemId = model.ItemId });
            }

            var today = DateTime.Today;

            // 2. Server-side Eligibility Verification:
            // Check for an approved, completed borrowing request belonging to this student for this item with no existing review
            var eligibleRequest = await _context.Requests
                .Where(r => r.ItemId == model.ItemId &&
                            r.RequesterId == currentUserId &&
                            r.Status == "Accepted" &&
                            r.EndDate.Date < today)
                .Where(r => !_context.Reviews.Any(rev => rev.BorrowRequestId == r.Id))
                .OrderByDescending(r => r.EndDate)
                .FirstOrDefaultAsync();

            if (eligibleRequest == null)
            {
                // Determine precise reason for informative feedback
                var activeBorrowing = await _context.Requests
                    .Where(r => r.ItemId == model.ItemId &&
                                r.RequesterId == currentUserId &&
                                r.Status == "Accepted" &&
                                r.EndDate.Date >= today)
                    .OrderBy(r => r.EndDate)
                    .FirstOrDefaultAsync();

                if (activeBorrowing != null)
                {
                    TempData["Error"] = $"You cannot submit a review while your borrowing period is still active (ends on {activeBorrowing.EndDate:MMM dd, yyyy}).";
                }
                else
                {
                    var alreadyReviewed = await _context.Requests
                        .AnyAsync(r => r.ItemId == model.ItemId &&
                                       r.RequesterId == currentUserId &&
                                       r.Status == "Accepted" &&
                                       r.EndDate.Date < today &&
                                       _context.Reviews.Any(rev => rev.BorrowRequestId == r.Id));

                    if (alreadyReviewed)
                    {
                        TempData["Error"] = "You have already submitted a review for this completed borrowing.";
                    }
                    else
                    {
                        TempData["Error"] = "You are not eligible to review this item. Only students who have completed an approved borrowing can write a review.";
                    }
                }

                return RedirectToAction(nameof(Index), new { itemId = model.ItemId });
            }

            // 3. Model validation
            if (!ModelState.IsValid)
            {
                var viewModel = await BuildReviewsViewModelAsync(model.ItemId, currentUserId);
                if (viewModel != null)
                {
                    viewModel.NewReview = model;
                    return View("Index", viewModel);
                }
            }

            // 4. Save review
            var review = new Review
            {
                ItemId = model.ItemId,
                ReviewerId = currentUserId,
                BorrowRequestId = eligibleRequest.Id,
                Rating = model.Rating,
                Comment = model.Comment.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your review has been submitted successfully!";
            return RedirectToAction(nameof(Index), new { itemId = model.ItemId });
        }

        // ==========================================
        // Helper: Build ItemReviewsViewModel
        // ==========================================
        private async Task<ItemReviewsViewModel?> BuildReviewsViewModelAsync(int itemId, string? currentUserId)
        {
            var item = await _context.Items
                .Include(i => i.Owner)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null)
            {
                return null;
            }

            var reviews = await _context.Reviews
                .Include(r => r.Reviewer)
                .Where(r => r.ItemId == itemId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var totalReviews = reviews.Count;
            var averageRating = totalReviews > 0
                ? Math.Round(reviews.Average(r => r.Rating), 1)
                : 0.0;

            var viewModel = new ItemReviewsViewModel
            {
                Item = item,
                Reviews = reviews,
                TotalReviews = totalReviews,
                AverageRating = averageRating,
                IsUserLoggedIn = !string.IsNullOrEmpty(currentUserId),
                NewReview = new CreateReviewViewModel
                {
                    ItemId = itemId,
                    Rating = 5
                }
            };

            if (string.IsNullOrEmpty(currentUserId))
            {
                return viewModel;
            }

            // Check if user is owner
            if (item.OwnerId == currentUserId)
            {
                viewModel.IsOwner = true;
                viewModel.IsEligibleToReview = false;
                viewModel.EligibilityMessage = "As the owner of this item, you cannot review your own item.";
                return viewModel;
            }

            var today = DateTime.Today;

            // Fetch all requests by this user for this item
            var userRequests = await _context.Requests
                .Where(r => r.ItemId == itemId && r.RequesterId == currentUserId)
                .OrderByDescending(r => r.EndDate)
                .ToListAsync();

            var acceptedRequests = userRequests
                .Where(r => r.Status == "Accepted")
                .ToList();

            if (!acceptedRequests.Any())
            {
                viewModel.IsEligibleToReview = false;
                return viewModel;
            }

            // Completed vs Active requests
            var completedRequests = acceptedRequests
                .Where(r => r.EndDate.Date < today)
                .ToList();

            var activeOrFutureRequests = acceptedRequests
                .Where(r => r.EndDate.Date >= today)
                .ToList();

            // Check which completed requests already have reviews
            var completedRequestIds = completedRequests.Select(r => r.Id).ToList();
            var reviewedRequestIds = await _context.Reviews
                .Where(rev => completedRequestIds.Contains(rev.BorrowRequestId))
                .Select(rev => rev.BorrowRequestId)
                .ToListAsync();

            var unreviewedCompletedRequests = completedRequests
                .Where(r => !reviewedRequestIds.Contains(r.Id))
                .ToList();

            if (unreviewedCompletedRequests.Any())
            {
                var nextEligible = unreviewedCompletedRequests.First();
                viewModel.IsEligibleToReview = true;
                viewModel.EligibleBorrowRequestId = nextEligible.Id;
                viewModel.NewReview.BorrowRequestId = nextEligible.Id;
            }
            else if (activeOrFutureRequests.Any())
            {
                var activeReq = activeOrFutureRequests.OrderBy(r => r.EndDate).First();
                viewModel.HasActiveBorrowing = true;
                viewModel.ActiveBorrowingEndDate = activeReq.EndDate;
                viewModel.EligibilityMessage = $"Your borrowing period for this item is currently active (ends on {activeReq.EndDate:MMM dd, yyyy}). You can submit a review once the borrowing duration has ended.";
            }
            else if (completedRequests.Any() && reviewedRequestIds.Count >= completedRequests.Count)
            {
                viewModel.HasAlreadyReviewedAll = true;
                viewModel.EligibilityMessage = "You have already submitted a review for your completed borrowing of this item.";
            }

            return viewModel;
        }
    }
}
