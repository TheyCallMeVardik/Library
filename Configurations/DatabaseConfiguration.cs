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
                entity.Property(e => e.IsAvailable).HasDefaultValue(true);
                entity.Property(e => e.Price).HasColumnType("decimal(10,2)");
            });

            builder.Entity<Models.Order>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Book)
                    .WithMany()
                    .HasForeignKey(e => e.BookId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.OrderDate).IsRequired();
            });
        }
    }
}
