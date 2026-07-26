using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Application.Validation;

namespace Report.Infrastructure.Services;

public sealed class ReportValidationService : IReportValidationService
{
    private readonly ISmartReportSnapshotService _snapshotService;
    private readonly IReportValidationEngine _engine;

    public ReportValidationService(
        ISmartReportSnapshotService snapshotService,
        IReportValidationEngine engine)
        => (_snapshotService, _engine) = (snapshotService, engine);

    public async Task<ReportValidationResult> ValidateAsync(
        ValidateReportRequest request, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotService.GetPreviewAsync(
            request.ClubId, request.SemesterId, cancellationToken);
        return _engine.Validate(request, snapshot);
    }
}
