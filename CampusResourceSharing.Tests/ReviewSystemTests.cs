using CampusResourceSharing.Controllers;
using CampusResourceSharing.Data;
using CampusResourceSharing.Models;
using CampusResourceSharing.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace CampusResourceSharing.Tests
{
    public class DummyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    public class ReviewSystemTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private ReviewController CreateController(ApplicationDbContext context, string? userId = null)
        {
            var controller = new ReviewController(context);
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

            var tempData = new TempDataDictionary(httpContext, new DummyTempDataProvider());
            controller.TempData = tempData;
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        private ApplicationUser CreateUser(string id, string name, string dept = "Engineering")
        {
            return new ApplicationUser
            {
                Id = id,
                UserName = $"{id}@campus.edu",
                Email = $"{id}@campus.edu",
                FullName = name,
                Department = dept,
                Address = "Campus Hostel"
            };
        }

        // 1. Student requests an item -> owner rejects -> student cannot review
        [Fact]
        public async Task Scenario1_RejectedBorrowRequest_CannotReview()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Dr. Owner"));
            context.Users.Add(CreateUser(studentId, "Student Requester"));

            var item = new Item
            {
                Id = 1,
                Name = "Calculus Textbook",
                Category = "Books",
                OwnerId = ownerId,
                Status = ItemStatus.Approved,
                IsAvailable = true
            };
            context.Items.Add(item);

            var request = new Request
            {
                Id = 10,
                ItemId = 1,
                RequesterId = studentId,
                Status = "Rejected",
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(-5)
            };
            context.Requests.Add(request);
            await context.SaveChangesAsync();

            var controller = CreateController(context, studentId);

            // Check GET eligibility
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);
            Assert.False(model.IsEligibleToReview);

            // Attempt POST submission
            var postResult = await controller.Create(new CreateReviewViewModel
            {
                ItemId = 1,
                BorrowRequestId = 10,
                Rating = 5,
                Comment = "Should not be allowed"
            }) as RedirectToActionResult;

            Assert.NotNull(postResult);
            Assert.Equal("Index", postResult.ActionName);
            Assert.Contains("not eligible", controller.TempData["Error"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

            // Verify no review saved
            Assert.Empty(context.Reviews);
        }

        // 2. Student requests an item -> owner approves -> borrowing is still active -> student cannot review
        [Fact]
        public async Task Scenario2_ActiveBorrowing_CannotReviewUntilDurationEnds()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Owner Sharma"));
            context.Users.Add(CreateUser(studentId, "Active Borrower"));

            var item = new Item
            {
                Id = 1,
                Name = "Scientific Calculator",
                Category = "Electronics",
                OwnerId = ownerId,
                Status = ItemStatus.Approved,
                IsAvailable = true
            };
            context.Items.Add(item);

            // Start was yesterday, ends in 2 days (currently active)
            var request = new Request
            {
                Id = 11,
                ItemId = 1,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(2)
            };
            context.Requests.Add(request);
            await context.SaveChangesAsync();

            var controller = CreateController(context, studentId);

            // GET
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);
            Assert.False(model.IsEligibleToReview);
            Assert.True(model.HasActiveBorrowing);
            Assert.NotNull(model.EligibilityMessage);
            Assert.Contains("currently active", model.EligibilityMessage, StringComparison.OrdinalIgnoreCase);

            // POST
            await controller.Create(new CreateReviewViewModel
            {
                ItemId = 1,
                BorrowRequestId = 11,
                Rating = 4,
                Comment = "Still using it"
            });

            Assert.Contains("still active", controller.TempData["Error"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.Empty(context.Reviews);
        }

        // 3 & 4. Borrowing duration ends -> student can write a review -> submitted review appears
        [Fact]
        public async Task Scenario3_And_4_CompletedBorrowing_CanSubmitReview_AndAppearsInList()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Item Owner"));
            context.Users.Add(CreateUser(studentId, "Aarav Patel", "Chemical Engineering"));

            var item = new Item
            {
                Id = 1,
                Name = "Lab Coat",
                Category = "Lab Equipment",
                OwnerId = ownerId,
                Status = ItemStatus.Approved,
                IsAvailable = true
            };
            context.Items.Add(item);

            // Completed borrowing (ended 3 days ago)
            var request = new Request
            {
                Id = 12,
                ItemId = 1,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-7),
                EndDate = DateTime.Today.AddDays(-3)
            };
            context.Requests.Add(request);
            await context.SaveChangesAsync();

            var controller = CreateController(context, studentId);

            // GET check eligibility
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);
            Assert.True(model.IsEligibleToReview);
            Assert.Equal(12, model.EligibleBorrowRequestId);

            // Submit review
            var postResult = await controller.Create(new CreateReviewViewModel
            {
                ItemId = 1,
                BorrowRequestId = 12,
                Rating = 5,
                Comment = "Excellent lab coat, perfectly clean and in great condition!"
            }) as RedirectToActionResult;

            Assert.NotNull(postResult);
            Assert.Equal("Index", postResult.ActionName);
            Assert.Contains("successfully", controller.TempData["Success"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

            // Verify in DB
            var savedReview = await context.Reviews.FirstOrDefaultAsync(r => r.ItemId == 1);
            Assert.NotNull(savedReview);
            Assert.Equal(5, savedReview.Rating);
            Assert.Equal(studentId, savedReview.ReviewerId);
            Assert.Equal(12, savedReview.BorrowRequestId);
            Assert.Equal("Excellent lab coat, perfectly clean and in great condition!", savedReview.Comment);

            // Verify appearing in reviews list
            var refreshResult = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(refreshResult);
            var refreshModel = refreshResult.Model as ItemReviewsViewModel;
            Assert.NotNull(refreshModel);
            Assert.Equal(1, refreshModel.TotalReviews);
            Assert.Equal(5.0, refreshModel.AverageRating);
            Assert.Single(refreshModel.Reviews);
            Assert.Equal("Aarav Patel", refreshModel.Reviews[0].Reviewer?.FullName);
        }

        // 5. Another student browsing the item can see the review
        [Fact]
        public async Task Scenario5_OtherStudentBrowsing_CanSeeReview()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var borrowerId = "student-1";
            var browserId = "student-2";

            context.Users.Add(CreateUser(ownerId, "Board Owner"));
            context.Users.Add(CreateUser(borrowerId, "Priya Sharma", "Civil Engineering"));
            context.Users.Add(CreateUser(browserId, "Rohan Verma", "Computer Engineering"));

            var item = new Item
            {
                Id = 1,
                Name = "Drawing Board",
                Category = "Drafting",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);

            var request = new Request
            {
                Id = 15,
                ItemId = 1,
                RequesterId = borrowerId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(-2)
            };
            context.Requests.Add(request);

            var review = new Review
            {
                Id = 1,
                ItemId = 1,
                ReviewerId = borrowerId,
                BorrowRequestId = 15,
                Rating = 4,
                Comment = "Very smooth board, helped a lot for CAD manual drafting.",
                CreatedAt = DateTime.Now.AddDays(-1)
            };
            context.Reviews.Add(review);
            await context.SaveChangesAsync();

            // Student 2 browses the reviews
            var controller = CreateController(context, browserId);
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);

            Assert.Equal(1, model.TotalReviews);
            Assert.Equal(4.0, model.AverageRating);
            Assert.Single(model.Reviews);
            Assert.Equal("Priya Sharma", model.Reviews[0].Reviewer?.FullName);
            Assert.Equal("Very smooth board, helped a lot for CAD manual drafting.", model.Reviews[0].Comment);
            // Browser never borrowed, so they cannot write a review
            Assert.False(model.IsEligibleToReview);
        }

        // 6. Student tries to submit a second review for the same borrowing -> prevent it
        [Fact]
        public async Task Scenario6_DuplicateReviewForSameBorrowing_Prevented()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Kit Owner"));
            context.Users.Add(CreateUser(studentId, "Student Borrower"));

            var item = new Item
            {
                Id = 1,
                Name = "Arduino Starter Kit",
                Category = "Electronics",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);

            var request = new Request
            {
                Id = 20,
                ItemId = 1,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-15),
                EndDate = DateTime.Today.AddDays(-5)
            };
            context.Requests.Add(request);

            // Existing review already submitted
            var existingReview = new Review
            {
                Id = 1,
                ItemId = 1,
                ReviewerId = studentId,
                BorrowRequestId = 20,
                Rating = 5,
                Comment = "Great kit, had all sensors!"
            };
            context.Reviews.Add(existingReview);
            await context.SaveChangesAsync();

            var controller = CreateController(context, studentId);

            // Check GET state: already reviewed
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);
            Assert.False(model.IsEligibleToReview);
            Assert.True(model.HasAlreadyReviewedAll);

            // Try to submit second review
            await controller.Create(new CreateReviewViewModel
            {
                ItemId = 1,
                BorrowRequestId = 20,
                Rating = 1,
                Comment = "Trying to submit duplicate"
            });

            Assert.Contains("already submitted a review", controller.TempData["Error"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            // Verify count remains 1
            Assert.Equal(1, await context.Reviews.CountAsync());
        }

        // 7. Student who never borrowed the item tries to submit a review -> prevent it
        [Fact]
        public async Task Scenario7_NeverBorrowed_CannotSubmitReview()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var unverifiedStudentId = "student-stranger";

            context.Users.Add(CreateUser(ownerId, "Guitar Owner"));
            context.Users.Add(CreateUser(unverifiedStudentId, "Stranger Student"));

            var item = new Item
            {
                Id = 1,
                Name = "Guitar",
                Category = "Music",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);
            await context.SaveChangesAsync();

            var controller = CreateController(context, unverifiedStudentId);

            // GET check
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);
            Assert.False(model.IsEligibleToReview);

            // POST forged request ID
            await controller.Create(new CreateReviewViewModel
            {
                ItemId = 1,
                BorrowRequestId = 9999,
                Rating = 5,
                Comment = "Fake review from non-borrower"
            });

            Assert.Contains("not eligible", controller.TempData["Error"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.Empty(context.Reviews);
        }

        // 8. Item with no reviews -> Reviews section displays "No reviews yet."
        [Fact]
        public async Task Scenario8_NoReviews_DisplaysZeroAndEmptyList()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            context.Users.Add(CreateUser(ownerId, "Microscope Owner"));

            var item = new Item
            {
                Id = 1,
                Name = "Microscope",
                Category = "Science",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);
            await context.SaveChangesAsync();

            var controller = CreateController(context, null); // anonymous visitor
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);

            Assert.Equal(0, model.TotalReviews);
            Assert.Equal(0.0, model.AverageRating);
            Assert.Empty(model.Reviews);
        }

        // 9. Average rating and total review count are calculated correctly
        [Fact]
        public async Task Scenario9_AverageRatingAndCountCalculatedCorrectly()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            context.Users.Add(CreateUser(ownerId, "Camera Owner"));
            context.Users.Add(CreateUser("s1", "Student 1"));
            context.Users.Add(CreateUser("s2", "Student 2"));
            context.Users.Add(CreateUser("s3", "Student 3"));

            var item = new Item
            {
                Id = 1,
                Name = "DSLR Camera",
                Category = "Photography",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);

            // 3 completed requests from different students
            context.Requests.AddRange(
                new Request { Id = 1, ItemId = 1, RequesterId = "s1", Status = "Accepted", StartDate = DateTime.Today.AddDays(-20), EndDate = DateTime.Today.AddDays(-15) },
                new Request { Id = 2, ItemId = 1, RequesterId = "s2", Status = "Accepted", StartDate = DateTime.Today.AddDays(-14), EndDate = DateTime.Today.AddDays(-10) },
                new Request { Id = 3, ItemId = 1, RequesterId = "s3", Status = "Accepted", StartDate = DateTime.Today.AddDays(-9), EndDate = DateTime.Today.AddDays(-5) }
            );

            // 3 reviews: 5 stars, 4 stars, 4 stars -> Avg = 13/3 = 4.333... -> Math.Round to 4.3
            context.Reviews.AddRange(
                new Review { Id = 1, ItemId = 1, ReviewerId = "s1", BorrowRequestId = 1, Rating = 5, Comment = "Superb camera" },
                new Review { Id = 2, ItemId = 1, ReviewerId = "s2", BorrowRequestId = 2, Rating = 4, Comment = "Good battery life" },
                new Review { Id = 3, ItemId = 1, ReviewerId = "s3", BorrowRequestId = 3, Rating = 4, Comment = "Very nice lenses" }
            );

            await context.SaveChangesAsync();

            var controller = CreateController(context, null);
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);

            Assert.Equal(3, model.TotalReviews);
            Assert.Equal(4.3, model.AverageRating);
        }

        // 10. Owner restriction: Owner cannot review own item
        [Fact]
        public async Task Scenario10_OwnerCannotReviewOwnItem()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            context.Users.Add(CreateUser(ownerId, "Projector Owner"));

            var item = new Item
            {
                Id = 1,
                Name = "Projector",
                Category = "Electronics",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId);

            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);

            Assert.True(model.IsOwner);
            Assert.False(model.IsEligibleToReview);
            Assert.Contains("owner", model.EligibilityMessage ?? "", StringComparison.OrdinalIgnoreCase);

            // Try POST as owner
            await controller.Create(new CreateReviewViewModel
            {
                ItemId = 1,
                BorrowRequestId = 1,
                Rating = 5,
                Comment = "Reviewing my own item"
            });

            Assert.Contains("owner", controller.TempData["Error"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.Empty(context.Reviews);
        }

        // 11. Student with multiple completed borrowings can submit 1 review per completed borrowing
        [Fact]
        public async Task Scenario11_MultipleCompletedBorrowings_CanReviewEachOne()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Item Sharer"));
            context.Users.Add(CreateUser(studentId, "Frequent Borrower"));

            var item = new Item
            {
                Id = 1,
                Name = "TI-84 Graphing Calculator",
                Category = "Electronics",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);

            // Two completed borrowings by the same student at different times
            var req1 = new Request
            {
                Id = 101,
                ItemId = 1,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today.AddDays(-20)
            };
            var req2 = new Request
            {
                Id = 102,
                ItemId = 1,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-15),
                EndDate = DateTime.Today.AddDays(-5)
            };
            context.Requests.AddRange(req1, req2);

            // First borrowing was already reviewed
            context.Reviews.Add(new Review
            {
                Id = 1,
                ItemId = 1,
                ReviewerId = studentId,
                BorrowRequestId = 101,
                Rating = 4,
                Comment = "First borrowing was good."
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, studentId);

            // Student should still be eligible for the second borrowing (req2)!
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);

            Assert.True(model.IsEligibleToReview);
            Assert.Equal(102, model.EligibleBorrowRequestId);

            // Submit review for second borrowing
            await controller.Create(new CreateReviewViewModel
            {
                ItemId = 1,
                BorrowRequestId = 102,
                Rating = 5,
                Comment = "Second borrowing was even better!"
            });

            Assert.Contains("successfully", controller.TempData["Success"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, await context.Reviews.CountAsync());
        }

        // 12. Anonymous browsing: Can view reviews without login, but cannot write review
        [Fact]
        public async Task Scenario12_AnonymousUser_CanViewReviews_CannotWriteReview()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Speaker Owner"));
            context.Users.Add(CreateUser(studentId, "Borrower"));

            var item = new Item
            {
                Id = 1,
                Name = "Bluetooth Speaker",
                Category = "Audio",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);

            context.Requests.Add(new Request
            {
                Id = 50,
                ItemId = 1,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-5),
                EndDate = DateTime.Today.AddDays(-2)
            });

            context.Reviews.Add(new Review
            {
                Id = 1,
                ItemId = 1,
                ReviewerId = studentId,
                BorrowRequestId = 50,
                Rating = 5,
                Comment = "Loud sound, worked great for our presentation."
            });
            await context.SaveChangesAsync();

            // Anonymous controller (no user)
            var controller = CreateController(context, null);
            var result = await controller.Index(1, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);

            Assert.False(model.IsUserLoggedIn);
            Assert.False(model.IsEligibleToReview);
            Assert.Equal(1, model.TotalReviews);
            Assert.Equal(5.0, model.AverageRating);
            Assert.Single(model.Reviews);
        }

        // 13. GET /Review/Create redirects gracefully to Reviews page or Items page without 404
        [Fact]
        public void Scenario13_GetCreate_RedirectsWithout404()
        {
            using var context = CreateInMemoryDbContext();
            var controller = CreateController(context, "student-1");

            // GET with itemId
            var resultWithId = controller.Create(itemId: 3) as RedirectToActionResult;
            Assert.NotNull(resultWithId);
            Assert.Equal("Index", resultWithId.ActionName);
            Assert.Equal(3, resultWithId.RouteValues?["itemId"]);

            // GET without itemId
            var resultWithoutId = controller.Create(itemId: null) as RedirectToActionResult;
            Assert.NotNull(resultWithoutId);
            Assert.Equal("Index", resultWithoutId.ActionName);
            Assert.Equal("Item", resultWithoutId.ControllerName);
        }

        // 14. Validation errors return View with ModelState errors, not a 404
        [Fact]
        public async Task Scenario14_PostCreate_InvalidInput_ReturnsViewWithValidationErrors_No404()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Textbook Owner"));
            context.Users.Add(CreateUser(studentId, "Reviewer"));

            var item = new Item
            {
                Id = 3,
                Name = "Computer Networks 6th edition",
                Category = "Books",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);

            context.Requests.Add(new Request
            {
                Id = 30,
                ItemId = 3,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(-3)
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, studentId);

            // Submitting blank comment and rating out of range
            var postResult = await controller.Create(new CreateReviewViewModel
            {
                ItemId = 3,
                BorrowRequestId = 30,
                Rating = 0,
                Comment = ""
            }) as ViewResult;

            Assert.NotNull(postResult);
            Assert.Equal("Index", postResult.ViewName);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey("Comment") || controller.ModelState.ContainsKey("NewReview.Comment"));
            Assert.True(controller.ModelState.ContainsKey("Rating") || controller.ModelState.ContainsKey("NewReview.Rating"));

            // Verify no review saved to DB
            Assert.Empty(context.Reviews);
        }

        // 15. Form submitted with NewReview. prefix (tag helper output) binds and saves successfully without 404
        [Fact]
        public async Task Scenario15_PostCreate_WithNewReviewPrefix_BindsAndSavesSuccessfully()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Book Owner"));
            context.Users.Add(CreateUser(studentId, "Eligible Borrower"));

            var item = new Item
            {
                Id = 3,
                Name = "Computer Networks 6th edition",
                Category = "Books",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);

            context.Requests.Add(new Request
            {
                Id = 31,
                ItemId = 3,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-14),
                EndDate = DateTime.Today.AddDays(-2)
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, studentId);

            // Simulate form submission with NewReview. prefix in Request.Form
            var formFields = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "NewReview.ItemId", "3" },
                { "NewReview.BorrowRequestId", "31" },
                { "NewReview.Rating", "4" },
                { "NewReview.Comment", "It is a nice book. Good condition." }
            };
            controller.HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
            controller.HttpContext.Request.Form = new FormCollection(formFields);

            // Controller action called with default (unbound) model
            var postResult = await controller.Create(new CreateReviewViewModel()) as RedirectToActionResult;

            Assert.NotNull(postResult);
            Assert.Equal("Index", postResult.ActionName);
            Assert.Equal(3, postResult.RouteValues?["itemId"]);
            Assert.Contains("successfully", controller.TempData["Success"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

            var savedReview = await context.Reviews.FirstOrDefaultAsync(r => r.ItemId == 3);
            Assert.NotNull(savedReview);
            Assert.Equal(4, savedReview.Rating);
            Assert.Equal("It is a nice book. Good condition.", savedReview.Comment);
            Assert.Equal(31, savedReview.BorrowRequestId);
            Assert.Equal(studentId, savedReview.ReviewerId);
        }

        // 16. Non-existent item redirects with user-friendly error instead of 404
        [Fact]
        public async Task Scenario16_PostCreate_NonExistentItem_RedirectsWithError_No404()
        {
            using var context = CreateInMemoryDbContext();
            var controller = CreateController(context, "student-1");

            var postResult = await controller.Create(new CreateReviewViewModel
            {
                ItemId = 99999,
                Rating = 5,
                Comment = "Testing non-existent item"
            }) as RedirectToActionResult;

            Assert.NotNull(postResult);
            Assert.Equal("Index", postResult.ActionName);
            Assert.Equal("Item", postResult.ControllerName);
            Assert.Contains("could not be found", controller.TempData["Error"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }

        // 17. BorrowingStatus dynamically evaluates Upcoming -> Ongoing -> Completed
        [Fact]
        public void Scenario17_ApprovedRequest_BorrowingStatus_TransitionsCorrectlyByDate()
        {
            var request = new Request
            {
                Id = 1,
                ItemId = 1,
                RequesterId = "student-1",
                Status = "Accepted",
                StartDate = new DateTime(2026, 10, 5),
                EndDate = new DateTime(2026, 10, 10)
            };

            // Before start date (Oct 4) -> Upcoming
            Assert.Equal("Upcoming", request.GetBorrowingStatus(new DateTime(2026, 10, 4)));

            // On start date (Oct 5) -> Ongoing
            Assert.Equal("Ongoing", request.GetBorrowingStatus(new DateTime(2026, 10, 5)));

            // Midway during borrowing (Oct 8) -> Ongoing
            Assert.Equal("Ongoing", request.GetBorrowingStatus(new DateTime(2026, 10, 8)));

            // On end date (Oct 10, borrowing day still active) -> Ongoing
            Assert.Equal("Ongoing", request.GetBorrowingStatus(new DateTime(2026, 10, 10)));

            // After end date has passed (Oct 11) -> Completed
            Assert.Equal("Completed", request.GetBorrowingStatus(new DateTime(2026, 10, 11)));

            // Long after end date -> Completed
            Assert.Equal("Completed", request.GetBorrowingStatus(new DateTime(2026, 10, 20)));
        }

        // 18. Pending and Rejected requests do not have a borrowing status
        [Fact]
        public void Scenario18_PendingAndRejectedRequests_DoNotHaveBorrowingStatus()
        {
            var pendingRequest = new Request
            {
                Id = 2,
                Status = "Pending",
                StartDate = DateTime.Today.AddDays(-5),
                EndDate = DateTime.Today.AddDays(5)
            };

            var rejectedRequest = new Request
            {
                Id = 3,
                Status = "Rejected",
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(-2)
            };

            Assert.Null(pendingRequest.BorrowingStatus);
            Assert.Equal("Pending", pendingRequest.DisplayStatus);

            Assert.Null(rejectedRequest.BorrowingStatus);
            Assert.Equal("Rejected", rejectedRequest.DisplayStatus);
        }

        // 19. Upcoming approved borrowing cannot be reviewed
        [Fact]
        public async Task Scenario19_UpcomingBorrowing_CannotBeReviewedUntilCompleted()
        {
            using var context = CreateInMemoryDbContext();
            var ownerId = "owner-1";
            var studentId = "student-1";

            context.Users.Add(CreateUser(ownerId, "Textbook Owner"));
            context.Users.Add(CreateUser(studentId, "Upcoming Borrower"));

            var item = new Item
            {
                Id = 10,
                Name = "Projector",
                Category = "Electronics",
                OwnerId = ownerId,
                Status = ItemStatus.Approved
            };
            context.Items.Add(item);

            // Approved, but starts in 3 days (Upcoming)
            context.Requests.Add(new Request
            {
                Id = 100,
                ItemId = 10,
                RequesterId = studentId,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(3),
                EndDate = DateTime.Today.AddDays(7)
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, studentId);

            // GET eligibility
            var result = await controller.Index(10, null) as ViewResult;
            Assert.NotNull(result);
            var model = result.Model as ItemReviewsViewModel;
            Assert.NotNull(model);
            Assert.False(model.IsEligibleToReview);
            Assert.Contains("not started yet", model.EligibilityMessage ?? "", StringComparison.OrdinalIgnoreCase);

            // Attempt POST submission
            var postResult = await controller.Create(new CreateReviewViewModel
            {
                ItemId = 10,
                BorrowRequestId = 100,
                Rating = 5,
                Comment = "Should not be able to review before borrowing starts"
            }) as RedirectToActionResult;

            Assert.NotNull(postResult);
            Assert.Contains("not started yet", controller.TempData["Error"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.Empty(context.Reviews);
        }

        // 20. Completed borrowing allows review
        [Fact]
        public void Scenario20_CompletedBorrowing_IdentifiedCorrectly()
        {
            var completedRequest = new Request
            {
                Id = 5,
                Status = "Accepted",
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(-2)
            };

            Assert.True(completedRequest.IsCompleted);
            Assert.False(completedRequest.IsOngoing);
            Assert.False(completedRequest.IsUpcoming);
            Assert.Equal("Completed", completedRequest.BorrowingStatus);
            Assert.Equal("Completed", completedRequest.DisplayStatus);
        }
    }
}
