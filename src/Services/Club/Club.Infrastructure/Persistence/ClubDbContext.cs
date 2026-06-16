using Microsoft.EntityFrameworkCore;
using Club.Domain.Entities;
using System;

namespace Club.Infrastructure.Persistence
{
    public class ClubDbContext : DbContext
    {
        public ClubDbContext(DbContextOptions<ClubDbContext> options) : base(options)
        {
        }

        public DbSet<Club.Domain.Entities.Club> Clubs { get; set; }
        public DbSet<Club.Domain.Entities.ClubMember> ClubMembers { get; set; }
        public DbSet<Club.Domain.Entities.Event> Events { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Club.Domain.Entities.Club>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                
                entity.HasMany(c => c.Members)
                      .WithOne(m => m.Club)
                      .HasForeignKey(m => m.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasMany(c => c.Events)
                      .WithOne(e => e.Club)
                      .HasForeignKey(e => e.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Club.Domain.Entities.ClubMember>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ClubId, e.UserId }).IsUnique();
            });

            modelBuilder.Entity<Club.Domain.Entities.Event>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            });

            // Seeding default club
            modelBuilder.Entity<Club.Domain.Entities.Club>().HasData(
                new Club.Domain.Entities.Club
                {
                    Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    Name = "FPTU Software Engineering Club (F-Code)",
                    AdvisorId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    IsActive = true
                }
            );

            // Seeding default club members
            modelBuilder.Entity<Club.Domain.Entities.ClubMember>().HasData(
                new Club.Domain.Entities.ClubMember
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    ClubId = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"), // manager1 ID
                    Role = Club.Domain.Enums.ClubRole.President,
                    Status = Club.Domain.Enums.MembershipStatus.Approved,
                    JoinedAt = new DateTime(2025, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new Club.Domain.Entities.ClubMember
                {
                    Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    ClubId = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"), // student1 ID
                    Role = Club.Domain.Enums.ClubRole.Member,
                    Status = Club.Domain.Enums.MembershipStatus.Approved,
                    JoinedAt = new DateTime(2025, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                }
            );
        }
    }
}
