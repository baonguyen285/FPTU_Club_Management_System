using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Persistence
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
        {
        }

        public DbSet<NotificationEntity> Notifications { get; set; }
        public DbSet<StreamProcessingFailure> StreamProcessingFailures { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NotificationEntity>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Message).IsRequired();
                entity.Property(e => e.TargetUrl).HasMaxLength(500);
                entity.HasIndex(e => new { e.SourceEventId, e.UserId }).IsUnique().HasFilter("[SourceEventId] IS NOT NULL");
            });

            modelBuilder.Entity<StreamProcessingFailure>(entity =>
            {
                entity.ToTable("StreamProcessingFailures");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StreamEntryId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastErrorCode).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastErrorMessage).IsRequired().HasMaxLength(2000);
                entity.HasIndex(e => e.StreamEntryId).IsUnique();
            });
        }
    }
}
