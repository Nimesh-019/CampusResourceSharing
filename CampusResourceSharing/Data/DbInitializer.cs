using CampusResourceSharing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CampusResourceSharing.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var configuration = scope.ServiceProvider
                .GetRequiredService<IConfiguration>();

            var logger = scope.ServiceProvider
                .GetService<ILoggerFactory>()?
                .CreateLogger("CampusResourceSharing.Data.DbInitializer");

            // Ensure Admin role exists
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            // Read Admin seed credentials from configuration
            var adminEmail = configuration["AdminSeed:Email"];
            var adminPassword = configuration["AdminSeed:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger?.LogInformation("Admin seed credentials (AdminSeed:Email / AdminSeed:Password) not provided. Skipping admin user creation.");
                return;
            }

            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            // Create Admin user if it does not exist
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Campus Administrator",
                    Address = "Administration Building, Campus",
                    Department = "Administration",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(
                    adminUser,
                    adminPassword
                );

                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
            else
            {
                // Ensure existing Admin user has the Admin role
                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}