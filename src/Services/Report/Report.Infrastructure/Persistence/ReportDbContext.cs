using Microsoft.EntityFrameworkCore;
using Report.Domain.Entities;

namespace Report.Infrastructure.Persistence
{
    public class ReportDbContext : DbContext
    {
        public ReportDbContext(DbContextOptions<ReportDbContext> options) : base(options)
        {
        }

        public DbSet<Report.Domain.Entities.Report> Reports { get; set; }
        public DbSet<ReportAttachment> ReportAttachments { get; set; }
        public DbSet<KpiRule> KpiRules { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Report.Domain.Entities.Report>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Status).HasConversion<int>().IsRequired();
                entity.Property(e => e.Type).HasConversion<int>().IsRequired();

                entity.HasMany(e => e.Attachments)
                      .WithOne(a => a.Report)
                      .HasForeignKey(a => a.ReportId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ReportAttachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            });

            modelBuilder.Entity<KpiRule>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Weight).HasColumnType("decimal(5,2)");
            });

            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EventType).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Payload).IsRequired();
                entity.Property(e => e.LegacyPayload);
                entity.Property(e => e.LastError).HasMaxLength(2000);
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.HasIndex(e => new { e.PublishedAtUtc, e.NextAttemptAtUtc });
            });
        }
    }
}
