using Report.Application.DTOs;
using Report.Domain.Enums;

namespace Report.Application.Interfaces;

public interface IReportDraftGenerator
{
    Task<GeneratedReportDraftBody> GenerateAsync(
        ReportGenerationSnapshot snapshot,
        ReportType reportType,
        CancellationToken cancellationToken = default);
}

public interface IReportDraftGenerationService
{
    Task<GeneratedReportDraft> GenerateAsync(
        GenerateReportDraftRequest request,
        CancellationToken cancellationToken = default);
}
