using Club.Domain.Enums;
using Shared.Kernel.Domain;

namespace Club.Domain.Entities;

public sealed class ClubApplication : BaseEntity
{
    public Guid ApplicantUserId { get; set; }
    public string ProposedClubName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Objectives { get; set; } = string.Empty;
    public string? EvidenceUrlsJson { get; set; }
    public ClubApplicationStatus Status { get; set; } = ClubApplicationStatus.PendingApproval;
    public string? ReviewFeedback { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public Guid? CreatedClubId { get; set; }
}
