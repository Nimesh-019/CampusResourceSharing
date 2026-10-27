using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace CampusResourceSharing.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class RequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _requestCreationLocks = new();
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _acceptLocks = new();

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

            if (item == null || item.IsDeleted)
            {
                return NotFound();
            }

            if (item.Status != ItemStatus.Approved)
            {
                TempData["Error"] = "This item is not approved for sharing.";
                return RedirectToAction("Index", "Item");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (item.OwnerId == currentUserId)
            {
                TempData["Error"] = "You cannot request your own item.";
                return RedirectToAction("Index", "Item");
            }

            if (!item.IsAvailable)
            {
                TempData["Error"] = "This item is currently unavailable.";
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

            if (item == null || item.IsDeleted)
            {
                return NotFound();
            }

            if (item.Status != ItemStatus.Approved)
            {
                TempData["Error"] = "This item is not approved for sharing.";
                return RedirectToAction("Index", "Item");
            }

            ViewBag.Item = item;

            // Prevent requesting own item
            if (item.OwnerId == currentUserId)
            {
                TempData["Error"] = "You cannot request your own item.";
                return RedirectToAction("Index", "Item");
            }

            if (!item.IsAvailable)
            {
                ViewBag.ErrorMessage = "This item is currently unavailable.";
                return View();
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

            // Concurrency protection against duplicate submissions and race conditions
            var lockKey = $"{currentUserId}_{itemId}";
            var semaphore = _requestCreationLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
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

                try
                {
                    _context.Requests.Add(request);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Catch duplicate submission race condition at DB level
                    var duplicateExists = await _context.Requests
                        .AnyAsync(r =>
                            r.ItemId == itemId &&
                            r.RequesterId == currentUserId &&
                            r.Status == "Pending" &&
                            r.StartDate.Date <= endDate.Date &&
                            r.EndDate.Date >= startDate.Date);

                    if (duplicateExists)
                    {
                        ViewBag.ErrorMessage = "You already have a pending request for this item during this duration.";
                        return View();
                    }
                    throw;
                }

                TempData["Success"] = "Item request sent successfully.";
                return RedirectToAction(nameof(MyRequests));
            }
            finally
            {
                semaphore.Release();
            }
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
        public async Task<IActionResult> Accept(int id, string? returnUrl = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var request = await _context.Requests
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == id && r.Item != null && r.Item.OwnerId == currentUserId);

            if (request == null)
            {
                return NotFound();
            }

            // Issue 5: State Machine Guard - Accept works ONLY when Status is "Pending"
            if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = $"Cannot accept this request because it is already marked as {request.Status}.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(IncomingRequests));
            }

            if (request.Item!.IsDeleted || !request.Item.IsAvailable)
            {
                TempData["Error"] = "This item is no longer available.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(IncomingRequests));
            }

            // Issue 2: Concurrency protection for overlapping request acceptance
            var semaphore = _acceptLocks.GetOrAdd(request.ItemId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
                if (_context.Database.IsRelational())
                {
                    transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                }

                try
                {
                    // Re-check inside transaction and lock:
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
                        if (transaction != null) await transaction.RollbackAsync();
                        TempData["Error"] = "Item is not available for that duration because another request is already accepted.";
                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        return RedirectToAction(nameof(IncomingRequests));
                    }

                    // Re-verify request is still pending
                    var freshRequest = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
                    if (freshRequest == null || !string.Equals(freshRequest.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        if (transaction != null) await transaction.RollbackAsync();
                        TempData["Error"] = "This request is no longer pending.";
                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        return RedirectToAction(nameof(IncomingRequests));
                    }

                    freshRequest.Status = "Accepted";
                    freshRequest.RespondedAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }

                    TempData["Success"] = "Request accepted successfully.";
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction(nameof(IncomingRequests));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    TempData["Error"] = "The request was modified concurrently. Please refresh and try again.";
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction(nameof(IncomingRequests));
                }
                catch (Exception)
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    throw;
                }
                finally
                {
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync();
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        // POST: Request/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? returnUrl = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var request = await _context.Requests
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == id && r.Item != null && r.Item.OwnerId == currentUserId);

            if (request == null)
            {
                return NotFound();
            }

            // Issue 5: State Machine Guard - Reject works ONLY when Status is "Pending"
            if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = $"Cannot reject this request because it is already marked as {request.Status}.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(IncomingRequests));
            }

            request.Status = "Rejected";
            request.RespondedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Request rejected.";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(IncomingRequests));
        }

        // GET: Request/MyRequests
        public async Task<IActionResult> MyRequests()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var requests = await _context.Requests
                .Include(r => r.Item!)
                    .ThenInclude(i => i.Owner)
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