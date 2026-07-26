using Report.Domain.Enums;

namespace Report.Application.DTOs;

public sealed class GenerateReportDraftRequest
{
    public Guid ClubId { get; set; }
    public Guid SemesterId { get; set; }
    public ReportType ReportType { get; set; }
}

public sealed record GeneratedReportDraft(
    Guid ClubId,
    Guid SemesterId,
    ReportType ReportType,
    string GeneratedTitle,
    string GeneratedContent,
    IReadOnlyList<SourceReference> Sources,
    ReportValidationResult Validation,
    string GeneratorType,
    string SnapshotVersion,
    DateTime GeneratedAt);

public sealed record GeneratedReportDraftBody(
    string GeneratedTitle,
    string GeneratedContent,
    IReadOnlyList<SourceReference> Sources,
    DateTime GeneratedAt);
