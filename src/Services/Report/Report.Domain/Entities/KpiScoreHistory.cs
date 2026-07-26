using Shared.Kernel.Domain;

namespace Report.Domain.Entities;

public sealed class KpiScoreHistory : BaseEntity
{
    public Guid ClubId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid? RuleId { get; set; }
    public decimal Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SourceType { get; set; } = "ManualAdjustment";
    public Guid? SourceId { get; set; }
    public Guid AdjustedBy { get; set; }
}
