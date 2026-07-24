using Shared.Kernel.Domain;

namespace Finance.Domain.Entities;

public class ClubFinanceBalance : BaseEntity
{
    public Guid ClubId { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal AvailableAmount { get; set; }
}
