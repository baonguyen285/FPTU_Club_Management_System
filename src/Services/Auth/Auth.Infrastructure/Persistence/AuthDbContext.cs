using Microsoft.EntityFrameworkCore;
using Auth.Domain.Entities;
using System;

namespace Auth.Infrastructure.Persistence
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EmailVerificationCode).HasMaxLength(20);
                entity.Property(e => e.ResetPasswordCode).HasMaxLength(20);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Token).IsRequired().HasMaxLength(250);
            });

            // Seeding dữ liệu ban đầu
            var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var advisorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var managerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var studentId = Guid.Parse("44444444-4444-4444-4444-444444444444");

            string passHash = BCrypt.Net.BCrypt.HashPassword("Fptu@123");

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = adminId,
                    Email = "admin@fpt.edu.vn",
                    PasswordHash = passHash,
                    FullName = "BQL CLB FPTU (Admin)",
                    Role = "StudentAffairsAdmin",
                    IsActive = true,
                    IsEmailVerified = true,
                    CreatedAt = new DateTime(2025, 5, 20, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = advisorId,
                    Email = "advisor1@fpt.edu.vn",
                    PasswordHash = passHash,
                    FullName = "Nguyen Van A (Cố vấn)",
                    Role = "StudentAffairsAdmin",
                    IsActive = true,
                    IsEmailVerified = true,
                    CreatedAt = new DateTime(2025, 5, 20, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = managerId,
                    Email = "manager1@fpt.edu.vn",
                    PasswordHash = passHash,
                    FullName = "Tran Thi B (Trưởng CLB)",
                    Role = "ClubManager",
                    IsActive = true,
                    IsEmailVerified = true,
                    CreatedAt = new DateTime(2025, 5, 20, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = studentId,
                    Email = "student1@fpt.edu.vn",
                    PasswordHash = passHash,
                    FullName = "Le Van C (Thành viên)",
                    Role = "Student",
                    IsActive = true,
                    IsEmailVerified = true,
                    CreatedAt = new DateTime(2025, 5, 20, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
