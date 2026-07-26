namespace Club.Application.DTOs;

public sealed record ClubApplicationDto(
    Guid Id,
    Guid ApplicantUserId,
    string ProposedClubName,
    string Description,
    string Objectives,
    IReadOnlyList<string> EvidenceUrls,
    string Status,
    string? ReviewFeedback,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    Guid? ReviewedByUserId,
    Guid? CreatedClubId);

public sealed record CreateClubApplicationRequest(
    string ProposedClubName,
    string Description,
    string Objectives,
    IReadOnlyList<string>? EvidenceUrls);

public sealed record UpdateClubApplicationRequest(
    string ProposedClubName,
    string Description,
    string Objectives,
    IReadOnlyList<string>? EvidenceUrls);

public sealed record RejectClubApplicationRequest(string Reason);
