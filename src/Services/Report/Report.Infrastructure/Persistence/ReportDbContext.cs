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
        public DbSet<Semester> Semesters { get; set; }
        public DbSet<ReportRevisionHistory> ReportRevisionHistories { get; set; }
        public DbSet<KpiScoreHistory> KpiScoreHistories { get; set; }

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
                entity.Property(e => e.RevisionNumber).HasDefaultValue(1);
                entity.HasOne<Semester>().WithMany().HasForeignKey(e => e.SemesterId).OnDelete(DeleteBehavior.Restrict);

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
                entity.HasIndex(e => new { e.SemesterId, e.Name }).IsUnique();
                entity.HasOne<Semester>().WithMany().HasForeignKey(e => e.SemesterId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ReportRevisionHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PreviousStatus).HasConversion<int>();
                entity.Property(e => e.NewStatus).HasConversion<int>();
                entity.Property(e => e.Feedback).HasMaxLength(1000);
                entity.HasIndex(e => new { e.ReportId, e.ChangedAt });
                entity.HasOne<Report.Domain.Entities.Report>().WithMany().HasForeignKey(e => e.ReportId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<KpiScoreHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Points).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
                entity.Property(e => e.SourceType).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => new { e.SemesterId, e.ClubId, e.CreatedAt });
                entity.HasIndex(e => new { e.SourceType, e.SourceId }).IsUnique().HasFilter("[SourceId] IS NOT NULL");
                entity.HasOne<Semester>().WithMany().HasForeignKey(e => e.SemesterId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<KpiRule>().WithMany().HasForeignKey(e => e.RuleId).OnDelete(DeleteBehavior.Restrict);
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

            modelBuilder.Entity<Semester>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Status).HasConversion<int>().IsRequired();
                entity.Property(e => e.StartDate).HasColumnType("datetime2");
                entity.Property(e => e.EndDate).HasColumnType("datetime2");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Status)
                    .IsUnique()
                    .HasFilter("[Status] = 1");
            });
        }
    }
}
