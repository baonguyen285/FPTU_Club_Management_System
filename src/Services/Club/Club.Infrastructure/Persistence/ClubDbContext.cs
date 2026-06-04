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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Club.Domain.Entities.Club>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
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
        }
    }
}
