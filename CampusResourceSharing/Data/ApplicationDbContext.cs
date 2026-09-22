using CampusResourceSharing.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampusResourceSharing.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        //public DbSet<Category> Categories { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Item> Items { get; set; }
    }
}