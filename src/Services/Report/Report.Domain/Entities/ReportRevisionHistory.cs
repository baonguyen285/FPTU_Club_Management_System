using Report.Domain.Enums;
using Shared.Kernel.Domain;

namespace Report.Domain.Entities;

public sealed class ReportRevisionHistory : BaseEntity
{
    public Guid ReportId { get; private set; }
    public int RevisionNumber { get; private set; }
    public ReportStatus PreviousStatus { get; private set; }
    public ReportStatus NewStatus { get; private set; }
    public string? Feedback { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTime ChangedAt { get; private set; }

    private ReportRevisionHistory() { }

    public ReportRevisionHistory(
        Guid reportId,
        int revisionNumber,
        ReportStatus previousStatus,
        ReportStatus newStatus,
        string? feedback,
        Guid changedBy)
    {
        Id = Guid.NewGuid();
        ReportId = reportId;
        RevisionNumber = revisionNumber;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        ChangedBy = changedBy;
        ChangedAt = DateTime.UtcNow;
    }
}
