using Report.Application.DTOs;

namespace Report.Application.Interfaces;

public interface ISmartReportSnapshotService
{
    Task<ReportGenerationSnapshot> GetPreviewAsync(
        Guid clubId, Guid semesterId, CancellationToken cancellationToken = default);
}

public interface IClubReportSnapshotSource
{
    Task<ClubSnapshotSourceData> GetAsync(
        Guid clubId, Guid semesterId, DateTime start, DateTime end,
        CancellationToken cancellationToken = default);
}

public interface IFinanceReportSnapshotSource
{
    Task<FinanceSnapshotSourceData> GetAsync(
        Guid clubId, Guid semesterId, DateTime start, DateTime end,
        CancellationToken cancellationToken = default);
}
