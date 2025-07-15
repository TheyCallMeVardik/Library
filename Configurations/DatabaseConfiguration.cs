using Microsoft.EntityFrameworkCore;

namespace Library.Configurations
{
    public class DatabaseConfiguration
    {
        public static void Configure(ModelBuilder builder)
        {
            builder.Entity<Models.Books>(entity =>
            {
                entity.ToTable("Books");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Author).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PublicationYear).IsRequired();
                entity.Property(e => e.Price).HasColumnType("decimal(10,2)");
            });
            builder.Entity<Models.UserBook>(entity =>
            {
                entity.ToTable("UserBooks");
                entity.HasKey(ub => ub.Id);
                entity.HasIndex(ub => new { ub.UserId, ub.BookId }).IsUnique(); 
                entity.Property(ub => ub.PurchaseDate).IsRequired();

                entity.HasOne(ub => ub.Book)
                    .WithMany() 
                    .HasForeignKey(ub => ub.BookId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ub => ub.ApplicationUser)
                    .WithMany() 
                    .HasForeignKey(ub => ub.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
