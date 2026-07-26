namespace Shared.Kernel.IntegrationEvents;

public sealed record IntegrationEventEnvelopeV1(
    Guid EventId,
    string EventType,
    string SchemaVersion,
    DateTime OccurredAtUtc,
    string SourceService,
    string CorrelationId,
    object Data);

public sealed record ReportSubmittedV1(
    Guid ReportId,
    Guid ClubId,
    Guid ReporterId,
    string ReportType,
    string Period,
    DateTime SubmittedAtUtc);

public sealed record ReportWorkflowEventV1(
    Guid ReportId,
    Guid ClubId,
    Guid SemesterId,
    string Status,
    int RevisionNumber,
    Guid ActorId,
    string? Feedback);

public sealed record ReportReminderDueV1(
    Guid ReportId,
    Guid ClubId,
    Guid ReporterId,
    DateTime ReminderDateUtc);

public sealed record ActivityCreatedV1(
    Guid ActivityId,
    Guid ClubId,
    string Title,
    DateTime ExpectedDateUtc);

public sealed record ClubApplicationReviewedV1(
    Guid ApplicationId,
    Guid ApplicantUserId,
    Guid? ClubId,
    string ProposedClubName,
    string Status,
    string? ReviewFeedback);

public sealed record BudgetWorkflowEventV1(
    Guid ProposalId,
    Guid ClubId,
    decimal RequestedAmount,
    decimal ApprovedAmount,
    decimal? ActualAmount,
    string Status,
    DateTime OccurredAtUtc);
