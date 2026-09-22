using CampusResourceSharing.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusResourceSharing.Controllers
{
    [Authorize]
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

            var items = await _context.Items
                .Where(i => i.IsAvailable && i.OwnerId != userId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}