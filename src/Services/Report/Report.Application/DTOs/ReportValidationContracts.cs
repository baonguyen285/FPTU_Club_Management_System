using Report.Domain.Enums;

namespace Report.Application.DTOs;

public enum ValidationSeverity
{
    Error = 1,
    Warning = 2,
    Suggestion = 3
}

public sealed class ValidateReportRequest
{
    public Guid ClubId { get; set; }
    public Guid SemesterId { get; set; }
    public ReportType ReportType { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public IReadOnlyList<ReportValidationAttachment>? Attachments { get; set; }
}

public sealed record ReportValidationAttachment(string Url, string FileName);

public sealed record ReportValidationIssue(
    string Code,
    ValidationSeverity Severity,
    string Message,
    string? Field = null,
    string? SourceType = null,
    string? SourceId = null,
    string? SourceTitle = null,
    string? SuggestedAction = null);

public sealed record ReportValidationResult(
    bool IsReadyToSubmit,
    IReadOnlyList<ReportValidationIssue> Errors,
    IReadOnlyList<ReportValidationIssue> Warnings,
    IReadOnlyList<ReportValidationIssue> Suggestions,
    DateTime EvaluatedAt,
    Guid ClubId,
    Guid SemesterId,
    string SnapshotVersion,
    ReportSnapshotAvailability Availability);
