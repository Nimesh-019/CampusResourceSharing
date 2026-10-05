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

        public async Task<IActionResult> Index(
            string? searchTerm,
            string? category,
            string? condition,
            string? sortBy = "Newest")
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.Items
                .Include(i => i.Owner)
                .Where(i => i.IsAvailable && i.Status == ItemStatus.Approved)
                .AsQueryable();

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
            ViewBag.SelectedSort = sortBy;

            return View(items);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}