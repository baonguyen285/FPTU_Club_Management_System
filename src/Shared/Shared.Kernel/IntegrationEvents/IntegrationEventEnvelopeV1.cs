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
