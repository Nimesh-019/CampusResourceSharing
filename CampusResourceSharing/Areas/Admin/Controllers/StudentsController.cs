using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusResourceSharing.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Admin/Students
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminIds = adminUsers.Select(u => u.Id).ToList();

            var students = await _context.Users
                .Where(u => !adminIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var studentItemCounts = await _context.Items
                .Where(i => !i.IsDeleted)
                .GroupBy(i => i.OwnerId)
                .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.OwnerId, g => g.Count);

            ViewBag.StudentItemCounts = studentItemCounts;

            return View(students);
        }

        // GET: /Admin/Students/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var student = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (student == null)
            {
                return NotFound();
            }

            var itemCount = await _context.Items.CountAsync(i => i.OwnerId == id && !i.IsDeleted);
            ViewBag.ItemCount = itemCount;

            return View(student);
        }

        // GET: /Admin/Students/Items/5
        [HttpGet]
        public async Task<IActionResult> Items(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var student = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (student == null)
            {
                return NotFound();
            }

            var items = await _context.Items
                .Where(i => i.OwnerId == id && !i.IsDeleted)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            ViewBag.Student = student;

            return View(items);
        }
    }
}
