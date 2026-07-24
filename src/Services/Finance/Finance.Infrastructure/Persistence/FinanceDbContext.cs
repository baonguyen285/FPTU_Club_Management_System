using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence;

public class FinanceDbContext : DbContext
{
    public FinanceDbContext(DbContextOptions<FinanceDbContext> options) : base(options)
    {
    }

    public DbSet<BudgetProposal> BudgetProposals => Set<BudgetProposal>();
    public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();
    public DbSet<ClubFinanceBalance> ClubFinanceBalances => Set<ClubFinanceBalance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BudgetProposal>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.RequestedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ApprovedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Feedback).HasMaxLength(1000);
            entity.Property(x => x.BudgetDetailsJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.ClubId, x.Status });
        });

        modelBuilder.Entity<FinanceTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Description).IsRequired().HasMaxLength(500);
            entity.Property(x => x.ReceiptUrl).HasMaxLength(1000);
            entity.HasIndex(x => new { x.ClubId, x.TransactionDate });
        });

        modelBuilder.Entity<ClubFinanceBalance>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ClubId).IsUnique();
            entity.Property(x => x.AllocatedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.SpentAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.AvailableAmount).HasColumnType("decimal(18,2)");
        });
    }
}
