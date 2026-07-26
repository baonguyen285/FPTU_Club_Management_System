using System.Globalization;
using Finance.Infrastructure.Persistence;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Grpc.SmartReports.V1;

namespace Finance.API.GrpcServices;

public sealed class FinanceReportSnapshotGrpcService : FinanceReportSnapshotSource.FinanceReportSnapshotSourceBase
{
    private readonly FinanceDbContext _db;

    public FinanceReportSnapshotGrpcService(FinanceDbContext db) => _db = db;

    public override async Task<FinanceReportDataReply> GetFinanceReportData(
        ReportSnapshotSourceRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ClubId, out var clubId)
            || !DateTime.TryParse(request.SemesterStartUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start)
            || !DateTime.TryParse(request.SemesterEndUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var end))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid snapshot source request."));

        var proposals = await _db.BudgetProposals.AsNoTracking()
            .Where(x => x.ClubId == clubId && x.ProposedDate >= start && x.ProposedDate <= end)
            .Select(x => new
            {
                x.Id, x.ActivityId, x.EventName, x.ProposedDate, x.Status,
                x.RequestedAmount, x.ApprovedAmount, x.ActualAmount
            })
            .ToListAsync(context.CancellationToken);
        var balance = await _db.ClubFinanceBalances.AsNoTracking()
            .Where(x => x.ClubId == clubId)
            .Select(x => (decimal?)x.AvailableAmount)
            .SingleOrDefaultAsync(context.CancellationToken);

        var reply = new FinanceReportDataReply
        {
            HasBalance = balance.HasValue,
            RemainingBalance = balance?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
        };
        reply.Items.AddRange(proposals.Select(x =>
        {
            var item = new SnapshotFinanceItem
            {
                Id = x.Id.ToString(), Title = x.EventName,
                ProposedDateUtc = x.ProposedDate.ToUniversalTime().ToString("O"),
                Status = x.Status.ToString(),
                RequestedAmount = x.RequestedAmount.ToString(CultureInfo.InvariantCulture)
            };
            if (x.ActivityId.HasValue) item.ActivityId = x.ActivityId.Value.ToString();
            if (x.ApprovedAmount.HasValue) item.ApprovedAmount = x.ApprovedAmount.Value.ToString(CultureInfo.InvariantCulture);
            if (x.ActualAmount.HasValue) item.ActualAmount = x.ActualAmount.Value.ToString(CultureInfo.InvariantCulture);
            return item;
        }));
        return reply;
    }
}
