using CampusResourceSharing.Areas.Admin.ViewModels;
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
    public class AdminController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        // GET: /Admin
        [HttpGet]
        [Route("Admin")]
        [Route("Admin/Index")]
        public async Task<IActionResult> Index()
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminIds = adminUsers.Select(u => u.Id).ToList();

            var totalStudents = await _context.Users
                .Where(u => !adminIds.Contains(u.Id))
                .CountAsync();

            var totalItems = await _context.Items.CountAsync();
            var pendingItems = await _context.Items.CountAsync(i => i.Status == ItemStatus.Pending);
            var approvedItems = await _context.Items.CountAsync(i => i.Status == ItemStatus.Approved);
            var rejectedItems = await _context.Items.CountAsync(i => i.Status == ItemStatus.Rejected);

            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalItems = totalItems;
            ViewBag.PendingItems = pendingItems;
            ViewBag.ApprovedItems = approvedItems;
            ViewBag.RejectedItems = rejectedItems;

            return View();
        }

        // GET: /Admin/Login
        [HttpGet]
        [AllowAnonymous]
        [Route("Admin/Login")]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
            {
                return RedirectToAction(nameof(Index));
            }

            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        // POST: /Admin/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Route("Admin/Login")]
        public async Task<IActionResult> Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid Admin credentials.");
                return View(model);
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin)
            {
                ModelState.AddModelError(string.Empty, "Access Denied. Account is not an Admin.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, "Invalid Admin credentials.");
            return View(model);
        }

        // POST: /Admin/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Admin/Logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }
    }
}
