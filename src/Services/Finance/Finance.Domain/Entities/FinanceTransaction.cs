using Finance.Domain.Enums;
using Shared.Kernel.Domain;

namespace Finance.Domain.Entities;

public class FinanceTransaction : BaseEntity
{
    public Guid ClubId { get; set; }
    public decimal Amount { get; set; }
    public FinanceTransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? ReceiptUrl { get; set; }
    public Guid CreatedBy { get; set; }
}
