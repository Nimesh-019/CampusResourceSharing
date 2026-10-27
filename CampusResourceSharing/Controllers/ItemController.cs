using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using CampusResourceSharing.Services;
using CampusResourceSharing.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusResourceSharing.Controllers
{
    [Authorize]
    public class ItemController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICloudinaryService _cloudinaryService;

        private const long MaxImageSize = 5 * 1024 * 1024; // 5 MB

        private readonly string[] _allowedExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        public ItemController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ICloudinaryService cloudinaryService)
        {
            _context = context;
            _environment = environment;
            _cloudinaryService = cloudinaryService;
        }

        // GET: Item
        // Shows available items shared by other students with search, filter, and sorting
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? category,
            string? condition,
            string? department,
            string? availability,
            string? sortBy = "Newest")
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.Items
                .Include(i => i.Owner)
                .Where(i => !i.IsDeleted && i.Status == ItemStatus.Approved)
                .AsQueryable();

            // Requirement 9: Exclude logged-in student's own items from available list to request
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(i => i.OwnerId != userId);
            }

            // Search by Item Name or Description
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(i =>
                    i.Name.ToLower().Contains(term) ||
                    (i.Description != null && i.Description.ToLower().Contains(term)));
            }

            // Filter by Category
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                query = query.Where(i => i.Category == category);
            }

            // Filter by Condition
            if (!string.IsNullOrWhiteSpace(condition) && condition != "All")
            {
                query = query.Where(i => i.Condition == condition);
            }

            // Filter by Owner's Department
            if (!string.IsNullOrWhiteSpace(department) && department != "All")
            {
                query = query.Where(i => i.Owner != null && i.Owner.Department == department);
            }

            // Filter by Availability (default to Available)
            if (string.IsNullOrWhiteSpace(availability) || availability == "Available")
            {
                query = query.Where(i => i.IsAvailable);
            }
            else if (availability == "Unavailable")
            {
                query = query.Where(i => !i.IsAvailable);
            }

            // Sort items
            switch (sortBy)
            {
                case "NameAsc":
                case "Name A-Z":
                case "NameA-Z":
                    query = query.OrderBy(i => i.Name);
                    sortBy = "NameAsc";
                    break;
                case "NameDesc":
                case "Name Z-A":
                case "NameZ-A":
                    query = query.OrderByDescending(i => i.Name);
                    sortBy = "NameDesc";
                    break;
                default:
                    query = query.OrderByDescending(i => i.CreatedAt);
                    sortBy = "Newest";
                    break;
            }

            var items = await query.ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedCategory = category ?? "All";
            ViewBag.SelectedCondition = condition ?? "All";
            ViewBag.SelectedDepartment = department ?? "All";
            ViewBag.SelectedAvailability = availability ?? "Available";
            ViewBag.SelectedSort = sortBy;

            return View(items);
        }

        // GET: Item/MyItems
        // Shows items shared by logged-in student
        public async Task<IActionResult> MyItems()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var items = await _context.Items
                .Where(i => i.OwnerId == userId && !i.IsDeleted)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        // GET: Item/History/5
        // Shows complete request history only for a specific item owned by the logged-in student
        public async Task<IActionResult> History(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .Include(i => i.Owner)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            // Enforce authorization: an owner should only be able to view the history of items that they own/shared
            if (item.OwnerId != userId)
            {
                TempData["Error"] = "You are only authorized to view the request history of your own items.";
                return RedirectToAction(nameof(MyItems));
            }

            var requests = await _context.Requests
                .Include(r => r.Requester)
                .Where(r => r.ItemId == id)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            var viewModel = new ItemHistoryViewModel
            {
                Item = item,
                Requests = requests
            };

            return View(viewModel);
        }

        // GET: Item/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var item = await _context.Items
                .Include(i => i.Owner)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Only allow viewing deleted items if current user is owner or admin
            if (item.IsDeleted && item.OwnerId != currentUserId && !isAdmin)
            {
                return NotFound();
            }

            // Only allow viewing non-approved items if current user is owner or admin
            if (item.Status != ItemStatus.Approved && item.OwnerId != currentUserId && !isAdmin)
            {
                return NotFound();
            }

            return View(item);
        }

        // GET: Item/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Item/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ItemCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId) || !await _context.Users.AnyAsync(u => u.Id == userId))
            {
                return Unauthorized();
            }

            if (model.Image != null)
            {
                if (!ValidateImage(model.Image))
                {
                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? imagePath = null;
            string? imagePublicId = null;

            if (model.Image != null)
            {
                var (url, publicId) = await _cloudinaryService.UploadImageAsync(model.Image);
                imagePath = url;
                imagePublicId = publicId;
            }

            var item = new Item
            {
                Name = model.Name,
                Description = model.Description,
                Category = model.Category,
                Condition = model.Condition,
                ImagePath = imagePath,
                ImagePublicId = imagePublicId,
                OwnerId = userId,
                IsAvailable = model.IsAvailable,
                Status = ItemStatus.Pending,
                CreatedAt = DateTime.Now
            };

            _context.Items.Add(item);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Item added successfully. It is now pending administrator approval.";
            return RedirectToAction(nameof(MyItems));
        }

        // GET: Item/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId && !i.IsDeleted);

            if (item == null)
            {
                return NotFound();
            }

            var model = new ItemEditViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Category = item.Category,
                Condition = item.Condition,
                IsAvailable = item.IsAvailable,
                ExistingImagePath = item.ImagePath
            };

            return View(model);
        }

        // POST: Item/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ItemEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var existingItem = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId && !i.IsDeleted);

            if (existingItem == null)
            {
                return NotFound();
            }

            if (model.Image != null)
            {
                if (!ValidateImage(model.Image))
                {
                    model.ExistingImagePath = existingItem.ImagePath;
                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                model.ExistingImagePath = existingItem.ImagePath;
                return View(model);
            }

            existingItem.Name = model.Name;
            existingItem.Description = model.Description;
            existingItem.Category = model.Category;
            existingItem.Condition = model.Condition;
            existingItem.IsAvailable = model.IsAvailable;
            existingItem.Status = ItemStatus.Pending; // Re-edited item must be approved again

            if (model.Image != null)
            {
                var oldPublicId = existingItem.ImagePublicId;
                var oldPath = existingItem.ImagePath;
                var (url, publicId) = await _cloudinaryService.UploadImageAsync(model.Image);
                existingItem.ImagePath = url;
                existingItem.ImagePublicId = publicId;
                await _cloudinaryService.DeleteImageAsync(oldPublicId, oldPath);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Item updated successfully and resubmitted for admin approval.";

            return RedirectToAction(nameof(MyItems));
        }

        // GET: Item/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId && !i.IsDeleted);

            if (item == null)
            {
                return NotFound();
            }

            // Check whether the item has any active (ongoing) or upcoming accepted borrowing
            var today = DateTime.Today;
            var activeOrUpcomingAcceptedRequests = await _context.Requests
                .Where(r => r.ItemId == item.Id && r.Status == "Accepted" && r.EndDate.Date >= today)
                .ToListAsync();

            if (activeOrUpcomingAcceptedRequests.Any())
            {
                var hasOngoing = activeOrUpcomingAcceptedRequests.Any(r => r.StartDate.Date <= today && r.EndDate.Date >= today);
                var hasUpcoming = activeOrUpcomingAcceptedRequests.Any(r => r.StartDate.Date > today);

                if (hasOngoing && hasUpcoming)
                {
                    TempData["Error"] = "Cannot delete this item because it has active and upcoming accepted borrowings. Please wait until all borrowings are completed.";
                }
                else if (hasOngoing)
                {
                    TempData["Error"] = "Cannot delete this item because it is currently being borrowed.";
                }
                else
                {
                    TempData["Error"] = "Cannot delete this item because it has an upcoming accepted borrowing.";
                }

                return RedirectToAction(nameof(MyItems));
            }

            return View(item);
        }

        // POST: Item/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId && !i.IsDeleted);

            if (item == null)
            {
                return NotFound();
            }

            // Server-side validation: Block deletion if item has active (ongoing) or upcoming accepted borrowings
            var today = DateTime.Today;
            var activeOrUpcoming = await _context.Requests
                .Where(r => r.ItemId == item.Id && r.Status == "Accepted" && r.EndDate.Date >= today)
                .ToListAsync();

            if (activeOrUpcoming.Any())
            {
                var hasOngoing = activeOrUpcoming.Any(r => r.StartDate.Date <= today && r.EndDate.Date >= today);
                var hasUpcoming = activeOrUpcoming.Any(r => r.StartDate.Date > today);

                if (hasOngoing && hasUpcoming)
                {
                    TempData["Error"] = "Cannot delete this item because it has active and upcoming accepted borrowings. Please wait until all borrowings are completed.";
                }
                else if (hasOngoing)
                {
                    TempData["Error"] = "Cannot delete this item because it is currently being borrowed.";
                }
                else
                {
                    TempData["Error"] = "Cannot delete this item because it has an upcoming accepted borrowing.";
                }

                return RedirectToAction(nameof(MyItems));
            }

            // Check if item has any borrowing history or reviews
            var hasRequests = await _context.Requests.AnyAsync(r => r.ItemId == item.Id);
            var hasReviews = await _context.Reviews.AnyAsync(r => r.ItemId == item.Id);

            if (hasRequests || hasReviews)
            {
                // Safe soft-delete / disable:
                // Preserves historical borrowing requests and reviews
                // Avoids cascade deletes and foreign-key exceptions
                item.IsDeleted = true;
                item.IsAvailable = false;

                // Reject any lingering pending requests since the item is removed
                var pendingRequests = await _context.Requests
                    .Where(r => r.ItemId == item.Id && r.Status == "Pending")
                    .ToListAsync();

                foreach (var req in pendingRequests)
                {
                    req.Status = "Rejected";
                    req.RespondedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Item has been removed and archived.";
            }
            else
            {
                // No requests and no reviews: safely physically delete and clean up image
                await _cloudinaryService.DeleteImageAsync(item.ImagePublicId, item.ImagePath);
                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Item removed successfully.";
            }

            return RedirectToAction(nameof(MyItems));
        }

        // ================================
        // IMAGE HELPERS
        // ================================

        private bool ValidateImage(IFormFile image)
        {
            if (image.Length > MaxImageSize)
            {
                ModelState.AddModelError("Image", "Image size cannot exceed 5 MB.");
                return false;
            }

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            if (!_allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Image", "Only JPG, JPEG, PNG, and WEBP images are allowed.");
                return false;
            }

            return true;
        }
    }
}