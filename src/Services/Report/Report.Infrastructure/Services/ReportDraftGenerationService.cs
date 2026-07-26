using Report.Application.DTOs;
using Report.Application.Generation;
using Report.Application.Interfaces;
using Report.Application.Validation;
using Shared.Kernel.Exceptions;

namespace Report.Infrastructure.Services;

public sealed class ReportDraftGenerationService : IReportDraftGenerationService
{
    private readonly ISmartReportSnapshotService _snapshotService;
    private readonly IReportDraftGenerator _generator;
    private readonly IReportValidationEngine _validationEngine;

    public ReportDraftGenerationService(
        ISmartReportSnapshotService snapshotService,
        IReportDraftGenerator generator,
        IReportValidationEngine validationEngine)
        => (_snapshotService, _generator, _validationEngine) =
            (snapshotService, generator, validationEngine);

    public async Task<GeneratedReportDraft> GenerateAsync(
        GenerateReportDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClubId == Guid.Empty) throw new BadRequestException("clubId is required.");
        if (request.SemesterId == Guid.Empty) throw new BadRequestException("semesterId is required.");
        if (!Enum.IsDefined(request.ReportType) || request.ReportType == 0)
            throw new BadRequestException("reportType is invalid.");

        var snapshot = await _snapshotService.GetPreviewAsync(
            request.ClubId, request.SemesterId, cancellationToken);
        var body = await _generator.GenerateAsync(snapshot, request.ReportType, cancellationToken);
        var validation = _validationEngine.Validate(
            new ValidateReportRequest
            {
                ClubId = request.ClubId,
                SemesterId = request.SemesterId,
                ReportType = request.ReportType,
                Title = body.GeneratedTitle,
                Content = body.GeneratedContent
            },
            snapshot);

        return new GeneratedReportDraft(
            request.ClubId,
            request.SemesterId,
            request.ReportType,
            body.GeneratedTitle,
            body.GeneratedContent,
            body.Sources,
            validation,
            RuleBasedReportDraftGenerator.GeneratorType,
            ReportValidationPolicy.SnapshotVersion,
            body.GeneratedAt);
    }
}
