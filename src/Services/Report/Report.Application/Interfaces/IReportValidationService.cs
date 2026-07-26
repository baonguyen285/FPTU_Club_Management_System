using Report.Application.DTOs;

namespace Report.Application.Interfaces;

public interface IReportValidationService
{
    Task<ReportValidationResult> ValidateAsync(
        ValidateReportRequest request, CancellationToken cancellationToken = default);
}
