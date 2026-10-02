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
        // Shows available items shared by other students with search and filter
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? category,
            string? condition,
            string? department,
            string? availability)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.Items
                .Include(i => i.Owner)
                .Where(i => i.Status == ItemStatus.Approved)
                .AsQueryable();

            // Requirement 9: Exclude logged-in student's own items from available list to request
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(i => i.OwnerId != userId);
            }

            // Search by Item Name or Description
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(i =>
                    i.Name.Contains(searchTerm) ||
                    (i.Description != null && i.Description.Contains(searchTerm)));
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

            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedCategory = category ?? "All";
            ViewBag.SelectedCondition = condition ?? "All";
            ViewBag.SelectedDepartment = department ?? "All";
            ViewBag.SelectedAvailability = availability ?? "Available";

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
                .Where(i => i.OwnerId == userId)
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
                .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId);

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
                .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId);

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
                await _cloudinaryService.DeleteImageAsync(existingItem.ImagePublicId, existingItem.ImagePath);
                var (url, publicId) = await _cloudinaryService.UploadImageAsync(model.Image);
                existingItem.ImagePath = url;
                existingItem.ImagePublicId = publicId;
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
                .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId);

            if (item == null)
            {
                return NotFound();
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
                .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId);

            if (item == null)
            {
                return NotFound();
            }

            await _cloudinaryService.DeleteImageAsync(item.ImagePublicId, item.ImagePath);
            _context.Items.Remove(item);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Item removed successfully.";

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