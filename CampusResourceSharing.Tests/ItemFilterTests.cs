using CampusResourceSharing.Controllers;
using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using CampusResourceSharing.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace CampusResourceSharing.Tests
{
    public class ItemFilterTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private ItemController CreateController(ApplicationDbContext context, string? userId = null)
        {
            var controller = new ItemController(context, null!, null!);
            var httpContext = new DefaultHttpContext();

            if (!string.IsNullOrEmpty(userId))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Name, $"user_{userId}@campus.edu")
                };
                var identity = new ClaimsIdentity(claims, "TestAuth");
                httpContext.User = new ClaimsPrincipal(identity);
            }
            else
            {
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        private async Task SeedSampleItemsAsync(ApplicationDbContext context)
        {
            var userCs = new ApplicationUser
            {
                Id = "user_cs",
                FullName = "Alice CS",
                Address = "Campus Dorm A",
                Department = "Computer Science",
                UserName = "alice@campus.edu",
                Email = "alice@campus.edu"
            };

            var userMech = new ApplicationUser
            {
                Id = "user_mech",
                FullName = "Bob Mech",
                Address = "Campus Dorm B",
                Department = "Mechanical Engineering",
                UserName = "bob@campus.edu",
                Email = "bob@campus.edu"
            };

            context.Users.AddRange(userCs, userMech);

            context.Items.AddRange(
                new Item
                {
                    Id = 1,
                    Name = "Dell Inspiron Laptop",
                    Description = "High performance laptop for coding",
                    Category = "Electronics",
                    Condition = "Good",
                    OwnerId = "user_cs",
                    IsAvailable = true,
                    Status = ItemStatus.Approved,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new Item
                {
                    Id = 2,
                    Name = "Calculus Textbook",
                    Description = "Math book with laptop stickers",
                    Category = "Books",
                    Condition = "Like New",
                    OwnerId = "user_cs",
                    IsAvailable = true,
                    Status = ItemStatus.Approved,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                new Item
                {
                    Id = 3,
                    Name = "Scientific Calculator",
                    Description = "Casio FX-991EX for lab and exams",
                    Category = "Calculators",
                    Condition = "New",
                    OwnerId = "user_mech",
                    IsAvailable = true,
                    Status = ItemStatus.Approved,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new Item
                {
                    Id = 4,
                    Name = "Mechanical Toolbox",
                    Description = "Wrench and screwdriver kit",
                    Category = "Tools",
                    Condition = "Fair",
                    OwnerId = "user_mech",
                    IsAvailable = true,
                    Status = ItemStatus.Approved,
                    CreatedAt = DateTime.UtcNow
                }
            );

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task Index_ReturnsAllApprovedAvailableItemsByDefault()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Index(null, null, null, null, null, "Newest") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Equal(4, items.Count);
        }

        [Fact]
        public async Task Index_FiltersBySearchTerm_InNameOrDescription()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            // "Laptop" appears in Dell Laptop (Name) and Calculus Textbook (Description)
            var result = await controller.Index("Laptop", null, null, null, null, "Newest") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Equal(2, items.Count);
            Assert.Contains(items, i => i.Name == "Dell Inspiron Laptop");
            Assert.Contains(items, i => i.Name == "Calculus Textbook");
        }

        [Fact]
        public async Task Index_FiltersByCategory()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Index(null, "Electronics", null, null, null, "Newest") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Single(items);
            Assert.Equal("Dell Inspiron Laptop", items[0].Name);
        }

        [Fact]
        public async Task Index_FiltersByDepartment()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Index(null, null, null, "Mechanical Engineering", null, "Newest") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Equal(2, items.Count);
            Assert.All(items, i => Assert.Equal("user_mech", i.OwnerId));
        }

        [Fact]
        public async Task Index_FiltersByCondition()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Index(null, null, "New", null, null, "Newest") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Single(items);
            Assert.Equal("Scientific Calculator", items[0].Name);
        }

        [Fact]
        public async Task Index_FiltersWorkTogether_SearchAndCategory()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            // User searches "Laptop" and selects "Electronics" -> only Dell Inspiron Laptop should match
            var result = await controller.Index("Laptop", "Electronics", null, null, null, "Newest") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Single(items);
            Assert.Equal("Dell Inspiron Laptop", items[0].Name);
        }

        [Fact]
        public async Task Index_SortsByNameAscending_NameAZ()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Index(null, null, null, null, null, "NameAsc") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Equal(4, items.Count);
            Assert.Equal("Calculus Textbook", items[0].Name);
            Assert.Equal("Dell Inspiron Laptop", items[1].Name);
            Assert.Equal("Mechanical Toolbox", items[2].Name);
            Assert.Equal("Scientific Calculator", items[3].Name);
        }

        [Fact]
        public async Task Index_SortsByNameDescending_NameZA()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Index(null, null, null, null, null, "NameDesc") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Equal(4, items.Count);
            Assert.Equal("Scientific Calculator", items[0].Name);
            Assert.Equal("Mechanical Toolbox", items[1].Name);
            Assert.Equal("Dell Inspiron Laptop", items[2].Name);
            Assert.Equal("Calculus Textbook", items[3].Name);
        }

        [Fact]
        public async Task Index_SortsByNewestByDefault()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Index(null, null, null, null, null, "Newest") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            Assert.Equal(4, items.Count);
            Assert.Equal("Mechanical Toolbox", items[0].Name);
            Assert.Equal("Scientific Calculator", items[1].Name);
        }

        [Fact]
        public async Task Index_ExcludesLoggedInUserOwnItems()
        {
            using var context = CreateInMemoryDbContext();
            await SeedSampleItemsAsync(context);
            // Log in as user_cs
            var controller = CreateController(context, "user_cs");

            var result = await controller.Index(null, null, null, null, null, "Newest") as ViewResult;

            Assert.NotNull(result);
            var items = Assert.IsAssignableFrom<List<Item>>(result.Model);
            // Only user_mech items should be returned
            Assert.Equal(2, items.Count);
            Assert.All(items, i => Assert.Equal("user_mech", i.OwnerId));
        }
    }
}
