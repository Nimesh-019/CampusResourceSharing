using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusResourceSharing.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.Items
                .Include(i => i.Owner)
                .Where(i => i.IsAvailable && i.Status == ItemStatus.Approved);

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(i => i.OwnerId != userId);
            }

            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .Take(6)
                .ToListAsync();

            return View(items);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}