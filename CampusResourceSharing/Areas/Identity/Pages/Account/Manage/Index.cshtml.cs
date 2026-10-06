using System.ComponentModel.DataAnnotations;
using CampusResourceSharing.Models;
using CampusResourceSharing.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CampusResourceSharing.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; } = string.Empty;

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public class InputModel
        {
            [Required(ErrorMessage = "Full Name is required.")]
            [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
            [Display(Name = "Full Name")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            [Display(Name = "Email Address")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Phone Number is required.")]
            [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required(ErrorMessage = "Address is required.")]
            [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
            [Display(Name = "Address")]
            public string Address { get; set; } = string.Empty;

            [Required(ErrorMessage = "College Branch / Department is required.")]
            [Display(Name = "College Branch / Department")]
            public string Department { get; set; } = string.Empty;
        }

        private async Task LoadAsync(ApplicationUser user, bool resetInput = false)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            Username = userName ?? string.Empty;

            if (resetInput)
            {
                Input = new InputModel
                {
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    Address = user.Address,
                    Department = user.Department
                };
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user, resetInput: true);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user, resetInput: false);
                return Page();
            }

            // Mobile number normalization & format validation
            if (!PhoneNumberHelper.TryNormalize(Input.PhoneNumber, out var normalizedPhone))
            {
                ModelState.AddModelError("Input.PhoneNumber", "Please enter a valid 10-digit mobile number.");
                await LoadAsync(user, resetInput: false);
                return Page();
            }

            var email = Input.Email.Trim();

            // Check if email was changed and is unique across OTHER users (case-insensitively)
            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUserByEmail = await _userManager.FindByEmailAsync(email);
                if (existingUserByEmail != null && existingUserByEmail.Id != user.Id)
                {
                    ModelState.AddModelError("Input.Email", "This email address is already registered.");
                }
            }

            // Check if phone was changed and is unique across OTHER users
            if (user.PhoneNumber != normalizedPhone)
            {
                var isPhoneTaken = await _userManager.Users.AnyAsync(u => u.PhoneNumber == normalizedPhone && u.Id != user.Id);
                if (isPhoneTaken)
                {
                    ModelState.AddModelError("Input.PhoneNumber", "This mobile number is already registered.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user, resetInput: false);
                return Page();
            }

            bool hasChanges = false;

            if (user.FullName != Input.FullName.Trim())
            {
                user.FullName = Input.FullName.Trim();
                hasChanges = true;
            }

            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                await _userManager.SetEmailAsync(user, email);
                await _userManager.SetUserNameAsync(user, email);
                hasChanges = true;
            }

            if (user.PhoneNumber != normalizedPhone)
            {
                user.PhoneNumber = normalizedPhone;
                hasChanges = true;
            }

            if (user.Address != Input.Address.Trim())
            {
                user.Address = Input.Address.Trim();
                hasChanges = true;
            }

            if (user.Department != Input.Department)
            {
                user.Department = Input.Department;
                hasChanges = true;
            }

            if (hasChanges)
            {
                try
                {
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        foreach (var error in updateResult.Errors)
                        {
                            if (error.Code == nameof(IdentityErrorDescriber.DuplicateEmail) ||
                                error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
                            {
                                ModelState.AddModelError("Input.Email", "This email address is already registered.");
                            }
                            else
                            {
                                ModelState.AddModelError(string.Empty, error.Description);
                            }
                        }
                        await LoadAsync(user, resetInput: false);
                        return Page();
                    }

                    await _signInManager.RefreshSignInAsync(user);
                    StatusMessage = "Your profile has been updated successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (DatabaseExceptionHelper.IsUniqueConstraintViolation(ex, out var field))
                    {
                        if (field == "PhoneNumber")
                        {
                            ModelState.AddModelError("Input.PhoneNumber", "This mobile number is already registered.");
                        }
                        else
                        {
                            ModelState.AddModelError("Input.Email", "This email address is already registered.");
                        }
                        await LoadAsync(user, resetInput: false);
                        return Page();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                StatusMessage = "No changes were made to your profile.";
            }

            return RedirectToPage();
        }
    }
}
