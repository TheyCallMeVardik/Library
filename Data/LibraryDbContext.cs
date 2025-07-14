using Library.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Library.Data
{
    public class LibraryDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Models.Books> Books { get; set; }
        public DbSet<Models.Order> Orders { get; set; }

        public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            Configurations.DatabaseConfiguration.Configure(builder); 
        }
    }
}
