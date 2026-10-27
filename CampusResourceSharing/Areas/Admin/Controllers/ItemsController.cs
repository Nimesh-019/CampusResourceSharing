using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using CampusResourceSharing.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusResourceSharing.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class ItemsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ItemsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/Items
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var items = await _context.Items
                .Include(i => i.Owner)
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        // GET: /Admin/Items/Pending
        [HttpGet]
        public async Task<IActionResult> Pending()
        {
            var items = await _context.Items
                .Include(i => i.Owner)
                .Where(i => !i.IsDeleted && i.Status == ItemStatus.Pending)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        // GET: /Admin/Items/Approved
        [HttpGet]
        public async Task<IActionResult> Approved()
        {       
            var items = await _context.Items
                .Include(i => i.Owner)
                .Where(i => !i.IsDeleted && i.Status == ItemStatus.Approved)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        // GET: /Admin/Items/History/5
        [HttpGet]
        public async Task<IActionResult> History(int? id)
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

        // GET: /Admin/Items/HistoryModal/5
        [HttpGet]
        public async Task<IActionResult> HistoryModal(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }

            var item = await _context.Items
                .Include(i => i.Owner)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound();
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

            return PartialView("_ItemHistoryModalPartial", viewModel);
        }

        // GET: /Admin/Items/Rejected
        [HttpGet]
        public async Task<IActionResult> Rejected()
        {
            var items = await _context.Items
                .Include(i => i.Owner)
                .Where(i => !i.IsDeleted && i.Status == ItemStatus.Rejected)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        // POST: /Admin/Items/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            item.Status = ItemStatus.Approved;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Item '{item.Name}' has been APPROVED.";
            return RedirectToAction(nameof(Pending));
        }

        // POST: /Admin/Items/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            item.Status = ItemStatus.Rejected;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Item '{item.Name}' has been REJECTED.";
            return RedirectToAction(nameof(Pending));
        }
    }
}
