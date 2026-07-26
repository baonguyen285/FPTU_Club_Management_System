namespace Report.Application.DTOs;

public sealed record ReportGenerationSnapshot(
    Guid ClubId,
    string ClubName,
    Guid SemesterId,
    string SemesterCode,
    int? TotalMembers,
    int? NewMembers,
    int? CompletedEvents,
    int? CancelledEvents,
    decimal? ApprovedBudget,
    decimal? ActualExpense,
    decimal? RemainingBalance,
    decimal? KpiScore,
    int? KpiRank,
    IReadOnlyList<ReportSnapshotEvent> Events,
    IReadOnlyList<ReportSnapshotFinanceItem> FinanceItems,
    IReadOnlyList<SourceReference> Sources,
    ReportSnapshotAvailability Availability);

public sealed record ReportSnapshotEvent(
    Guid Id, string Title, DateTime ExpectedDate, string Status);

public sealed record ReportSnapshotFinanceItem(
    Guid Id, Guid? ActivityId, string Title, DateTime ProposedDate, string Status,
    decimal RequestedAmount, decimal? ApprovedAmount, decimal? ActualAmount);

public sealed record SourceReference(
    string Type, string Id, string Title, string? Route = null);

public sealed record ReportSnapshotAvailability(
    bool Club,
    bool Membership,
    bool Events,
    bool Finance,
    bool Balance,
    bool Kpi);

public sealed record ClubSnapshotSourceData(
    string ClubName,
    IReadOnlyList<SnapshotMemberData> Members,
    IReadOnlyList<SnapshotEventData> Events);

public sealed record SnapshotMemberData(
    Guid Id, Guid UserId, int Role, int Status, DateTime JoinedAt, bool IsActive);

public sealed record SnapshotEventData(
    Guid Id, string Title, DateTime ExpectedDate, int Status, bool IsActive);

public sealed record FinanceSnapshotSourceData(
    IReadOnlyList<SnapshotFinanceData> Items,
    decimal? RemainingBalance);

public sealed record SnapshotFinanceData(
    Guid Id, Guid? ActivityId, string Title, DateTime ProposedDate, string Status,
    decimal RequestedAmount, decimal? ApprovedAmount, decimal? ActualAmount);
