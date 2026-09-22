using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusResourceSharing.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class RequestController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RequestController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Request/Create/5
        // 5 = ItemId
        [HttpGet]
        public async Task<IActionResult> Create(int itemId)
        {
            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null)
            {
                return Content(
                    $"RequestController reached successfully, but Item with ID {itemId} was not found in the database."
                );
            }

            var currentUserId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (item.OwnerId == currentUserId)
            {
                TempData["Error"] =
                    "You cannot request your own item.";

                return RedirectToAction("Index", "Item");
            }

            if (!item.IsAvailable)
            {
                TempData["Error"] =
                    "This item is currently unavailable.";

                return RedirectToAction("Index", "Item");
            }

            ViewBag.Item = item;

            return View();
        }

        // POST: Request/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int itemId, DateTime startDate, DateTime endDate, string? message)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null)
            {
                return NotFound();
            }

            ViewBag.Item = item;

            // Prevent requesting own item
            if (item.OwnerId == currentUserId)
            {
                TempData["Error"] = "You cannot request your own item.";
                return RedirectToAction("Index", "Item");
            }

            // Validate date logic
            if (startDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("StartDate", "Start date cannot be in the past.");
            }

            if (endDate.Date < startDate.Date)
            {
                ModelState.AddModelError("EndDate", "End date must be on or after the start date.");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            // Check item overall availability and check for overlapping accepted requests
            var isOverlapping = await _context.Requests
                .AnyAsync(r =>
                    r.ItemId == itemId &&
                    r.Status == "Accepted" &&
                    r.StartDate.Date <= endDate.Date &&
                    r.EndDate.Date >= startDate.Date);

            if (!item.IsAvailable || isOverlapping)
            {
                ViewBag.ErrorMessage = "Item is not available for that duration.";
                return View();
            }

            // Check for duplicate pending request for overlapping duration
            var existingPendingRequest = await _context.Requests
                .AnyAsync(r =>
                    r.ItemId == itemId &&
                    r.RequesterId == currentUserId &&
                    r.Status == "Pending" &&
                    r.StartDate.Date <= endDate.Date &&
                    r.EndDate.Date >= startDate.Date);

            if (existingPendingRequest)
            {
                ViewBag.ErrorMessage = "You already have a pending request for this item during this duration.";
                return View();
            }

            var request = new Request
            {
                ItemId = itemId,
                RequesterId = currentUserId!,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Message = message,
                Status = "Pending",
                RequestedAt = DateTime.Now
            };

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Item request sent successfully.";

            return RedirectToAction(nameof(MyRequests));
        }

        // GET: Request/IncomingRequests
        public async Task<IActionResult> IncomingRequests()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var incomingRequests = await _context.Requests
                .Include(r => r.Item)
                .Include(r => r.Requester)
                .Where(r => r.Item != null && r.Item.OwnerId == currentUserId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return View(incomingRequests);
        }

        // POST: Request/Accept/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var request = await _context.Requests
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == id && r.Item != null && r.Item.OwnerId == currentUserId);

            if (request == null)
            {
                return NotFound();
            }

            // Check if there is already an accepted request overlapping with this duration
            var isOverlapping = await _context.Requests
                .AnyAsync(r =>
                    r.ItemId == request.ItemId &&
                    r.Id != request.Id &&
                    r.Status == "Accepted" &&
                    r.StartDate.Date <= request.EndDate.Date &&
                    r.EndDate.Date >= request.StartDate.Date);

            if (isOverlapping)
            {
                TempData["Error"] = "Item is not available for that duration because another request is already accepted.";
                return RedirectToAction(nameof(IncomingRequests));
            }

            request.Status = "Accepted";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Request accepted successfully.";
            return RedirectToAction(nameof(IncomingRequests));
        }

        // POST: Request/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var request = await _context.Requests
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == id && r.Item != null && r.Item.OwnerId == currentUserId);

            if (request == null)
            {
                return NotFound();
            }

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Request rejected.";
            return RedirectToAction(nameof(IncomingRequests));
        }

        // GET: Request/MyRequests
        public async Task<IActionResult> MyRequests()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var requests = await _context.Requests
                .Include(r => r.Item)
                .Where(r => r.RequesterId == currentUserId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return View(requests);
        }

        // POST: Request/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var request = await _context.Requests
                .FirstOrDefaultAsync(r => r.Id == id && r.RequesterId == currentUserId && r.Status == "Pending");

            if (request != null)
            {
                _context.Requests.Remove(request);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Request cancelled successfully.";
            }

            return RedirectToAction(nameof(MyRequests));
        }
    }
}