using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using CampusResourceSharing.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusResourceSharing.Controllers
{
    //[Authorize]
    public class ItemController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

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
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Item
        // Shows items shared by other students
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            Console.WriteLine("Logged User ID: " + userId);

            if (userId == null)
            {
                return Unauthorized();
            }

            var items = await _context.Items
                .Where(i => i.IsAvailable && i.OwnerId != userId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            foreach (var item in items)
            {
                Console.WriteLine(
                    $"Item: {item.Name}, OwnerId: {item.OwnerId}");
            }

            return View(items);
        }

        // GET: Item/MyItems
        // Shows items shared by logged-in student
        public async Task<IActionResult> MyItems()
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

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

        // GET: Item/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
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
        public async Task<IActionResult> Create(
            ItemCreateViewModel model)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            // Validate image
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

            // Save image
            if (model.Image != null)
            {
                imagePath = await SaveImageAsync(model.Image);
            }

            var item = new Item
            {
                Name = model.Name,
                Description = model.Description,
                Category = model.Category,
                Condition = model.Condition,
                ImagePath = imagePath,
                OwnerId = userId,
                IsAvailable = true,
                CreatedAt = DateTime.Now
            };

            _context.Items.Add(item);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyItems));
        }

        // GET: Item/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.OwnerId == userId);

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
        public async Task<IActionResult> Edit(
            int id,
            ItemEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var existingItem = await _context.Items
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.OwnerId == userId);

            if (existingItem == null)
            {
                return NotFound();
            }

            // Validate new image
            if (model.Image != null)
            {
                if (!ValidateImage(model.Image))
                {
                    model.ExistingImagePath =
                        existingItem.ImagePath;

                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                model.ExistingImagePath =
                    existingItem.ImagePath;

                return View(model);
            }

            // Update normal fields
            existingItem.Name = model.Name;
            existingItem.Description = model.Description;
            existingItem.Category = model.Category;
            existingItem.Condition = model.Condition;
            existingItem.IsAvailable = model.IsAvailable;

            // If a new image was selected
            if (model.Image != null)
            {
                // Delete old image
                DeleteImage(existingItem.ImagePath);

                // Save new image
                existingItem.ImagePath =
                    await SaveImageAsync(model.Image);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyItems));
        }

        // GET: Item/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.OwnerId == userId);

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
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var item = await _context.Items
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.OwnerId == userId);

            if (item == null)
            {
                return NotFound();
            }

            // Delete image from wwwroot
            DeleteImage(item.ImagePath);

            _context.Items.Remove(item);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyItems));
        }

        // ================================
        // IMAGE HELPERS
        // ================================

        private bool ValidateImage(IFormFile image)
        {
            if (image.Length > MaxImageSize)
            {
                ModelState.AddModelError(
                    "Image",
                    "Image size cannot exceed 5 MB.");

                return false;
            }

            var extension = Path
                .GetExtension(image.FileName)
                .ToLowerInvariant();

            if (!_allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "Image",
                    "Only JPG, JPEG, PNG, and WEBP images are allowed.");

                return false;
            }

            return true;
        }

        private async Task<string> SaveImageAsync(
            IFormFile image)
        {
            var uploadsFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "items");

            Directory.CreateDirectory(uploadsFolder);

            var extension = Path
                .GetExtension(image.FileName)
                .ToLowerInvariant();

            var fileName =
                $"{Guid.NewGuid()}{extension}";

            var filePath = Path.Combine(
                uploadsFolder,
                fileName);

            using (var stream = new FileStream(
                filePath,
                FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            return $"/uploads/items/{fileName}";
        }

        private void DeleteImage(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                return;
            }

            var fileName = Path.GetFileName(imagePath);

            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            var filePath = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "items",
                fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}