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

        // GET: /Review or /Review/Index?itemId=5 or /Review/Index/5
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(int? id, int? itemId)
        {
            int targetItemId = itemId ?? id ?? 0;
            if (targetItemId <= 0)
            {
                return RedirectToAction("Index", "Item");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var viewModel = await BuildReviewsViewModelAsync(targetItemId, currentUserId);

            if (viewModel == null)
            {
                TempData["Error"] = "Item not found.";
                return RedirectToAction("Index", "Item");
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

        // GET: /Review/Create (friendly redirect to Reviews page if user navigates or refreshes)
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Create(int? itemId)
        {
            if (itemId.HasValue && itemId.Value > 0)
            {
                return RedirectToAction(nameof(Index), new { itemId = itemId.Value });
            }
            return RedirectToAction("Index", "Item");
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

            model ??= new CreateReviewViewModel();

            // Consolidate input from any binding source (prefix "NewReview", standard "model", or direct form fields)
            if (Request?.HasFormContentType == true)
            {
                if (int.TryParse(Request.Form["NewReview.ItemId"], out int formItemId) && formItemId > 0)
                    model.ItemId = formItemId;
                else if (int.TryParse(Request.Form["ItemId"], out int fItemId) && fItemId > 0)
                    model.ItemId = fItemId;

                if (int.TryParse(Request.Form["NewReview.BorrowRequestId"], out int formReqId) && formReqId > 0)
                    model.BorrowRequestId = formReqId;
                else if (int.TryParse(Request.Form["BorrowRequestId"], out int fReqId) && fReqId > 0)
                    model.BorrowRequestId = fReqId;

                if (int.TryParse(Request.Form["NewReview.Rating"], out int formRating) && formRating > 0)
                    model.Rating = formRating;
                else if (int.TryParse(Request.Form["Rating"], out int fRating) && fRating > 0)
                    model.Rating = fRating;

                var formComment = Request.Form["NewReview.Comment"].ToString();
                if (string.IsNullOrWhiteSpace(formComment))
                    formComment = Request.Form["Comment"].ToString();

                if (!string.IsNullOrWhiteSpace(formComment))
                    model.Comment = formComment;
            }

            // Fallback: check query string or route values if ItemId is still 0
            if (model.ItemId <= 0)
            {
                if (int.TryParse(Request?.Query?["itemId"], out int qItemId) && qItemId > 0)
                {
                    model.ItemId = qItemId;
                }
                else if (RouteData?.Values != null && RouteData.Values.TryGetValue("itemId", out var rVal) && int.TryParse(rVal?.ToString(), out int rItemId) && rItemId > 0)
                {
                    model.ItemId = rItemId;
                }
            }

            if (model.ItemId <= 0)
            {
                TempData["Error"] = "Item not specified.";
                return RedirectToAction("Index", "Item");
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == model.ItemId);

            if (item == null)
            {
                TempData["Error"] = "The item you are reviewing could not be found.";
                return RedirectToAction("Index", "Item");
            }

            // 1. Owner restriction: Owner cannot review their own item
            if (item.OwnerId == currentUserId)
            {
                TempData["Error"] = "As the owner of this item, you cannot review your own item.";
                return RedirectToAction(nameof(Index), new { itemId = item.Id });
            }

            var today = DateTime.Today;

            // 2. Server-side Eligibility Verification:
            // Check for an approved, completed borrowing request belonging to this student for this item with no existing review
            var eligibleRequest = await _context.Requests
                .Where(r => r.ItemId == item.Id &&
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
                    .Where(r => r.ItemId == item.Id &&
                                r.RequesterId == currentUserId &&
                                r.Status == "Accepted" &&
                                r.EndDate.Date >= today)
                    .OrderBy(r => r.EndDate)
                    .FirstOrDefaultAsync();

                if (activeBorrowing != null)
                {
                    if (activeBorrowing.StartDate.Date > today)
                    {
                        TempData["Error"] = $"Your approved borrowing period for this item has not started yet (scheduled from {activeBorrowing.StartDate:MMM dd, yyyy} to {activeBorrowing.EndDate:MMM dd, yyyy}). You can submit a review once the borrowing duration has ended.";
                    }
                    else
                    {
                        TempData["Error"] = $"You cannot submit a review while your borrowing period is still active (ends on {activeBorrowing.EndDate:MMM dd, yyyy}).";
                    }
                }
                else
                {
                    var alreadyReviewed = await _context.Requests
                        .AnyAsync(r => r.ItemId == item.Id &&
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

                return RedirectToAction(nameof(Index), new { itemId = item.Id });
            }

            // 3. Validation: Rating (1-5) and Comment (non-empty, min 3 chars)
            if (model.Rating < 1 || model.Rating > 5)
            {
                ModelState.AddModelError("Rating", "Please select a rating between 1 and 5 stars.");
                ModelState.AddModelError("NewReview.Rating", "Please select a rating between 1 and 5 stars.");
            }

            if (string.IsNullOrWhiteSpace(model.Comment) || model.Comment.Trim().Length < 3)
            {
                ModelState.AddModelError("Comment", "Review comment is required and must be at least 3 characters.");
                ModelState.AddModelError("NewReview.Comment", "Review comment is required and must be at least 3 characters.");
            }

            // Synchronize ModelState errors between prefixed and non-prefixed keys so tag helpers find them
            if (ModelState.ContainsKey("Comment") && !ModelState.ContainsKey("NewReview.Comment"))
            {
                foreach (var err in ModelState["Comment"]!.Errors)
                    ModelState.AddModelError("NewReview.Comment", err.ErrorMessage);
            }
            if (ModelState.ContainsKey("Rating") && !ModelState.ContainsKey("NewReview.Rating"))
            {
                foreach (var err in ModelState["Rating"]!.Errors)
                    ModelState.AddModelError("NewReview.Rating", err.ErrorMessage);
            }

            if (!ModelState.IsValid)
            {
                var viewModel = await BuildReviewsViewModelAsync(item.Id, currentUserId);
                if (viewModel != null)
                {
                    viewModel.NewReview = model;
                    return View("Index", viewModel);
                }
                TempData["Error"] = "Please provide a valid rating (1-5 stars) and a review comment.";
                return RedirectToAction(nameof(Index), new { itemId = item.Id });
            }

            // 4. Save review
            var review = new Review
            {
                ItemId = item.Id,
                ReviewerId = currentUserId,
                BorrowRequestId = eligibleRequest.Id,
                Rating = model.Rating,
                Comment = model.Comment.Trim(),
                CreatedAt = DateTime.Now
            };

            try
            {
                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Your review has been submitted successfully!";
            }
            catch (DbUpdateException)
            {
                // Preserve the existing one-review-per-borrowing-request rule.
                // Catch the expected duplicate-key/DbUpdateException scenario for concurrent/double submissions.
                var alreadyReviewed = await _context.Reviews.AnyAsync(r => r.BorrowRequestId == eligibleRequest.Id);
                if (alreadyReviewed)
                {
                    TempData["Error"] = "You have already submitted a review for this completed borrowing.";
                    return RedirectToAction(nameof(Index), new { itemId = item.Id });
                }

                // Do not hide unrelated database errors
                throw;
            }

            return RedirectToAction(nameof(Index), new { itemId = item.Id });
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
                if (activeReq.StartDate.Date > today)
                {
                    viewModel.EligibilityMessage = $"Your approved borrowing period for this item has not started yet (scheduled for {activeReq.StartDate:MMM dd, yyyy} - {activeReq.EndDate:MMM dd, yyyy}). You can submit a review once the borrowing duration has ended.";
                }
                else
                {
                    viewModel.EligibilityMessage = $"Your borrowing period for this item is currently active (ends on {activeReq.EndDate:MMM dd, yyyy}). You can submit a review once the borrowing duration has ended.";
                }
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
