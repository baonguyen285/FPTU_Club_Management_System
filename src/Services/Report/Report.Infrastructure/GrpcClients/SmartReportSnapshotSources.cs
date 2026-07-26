using System.Globalization;
using Grpc.Core;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Grpc.SmartReports.V1;

namespace Report.Infrastructure.GrpcClients;

public sealed class ClubReportSnapshotSourceClient : IClubReportSnapshotSource
{
    private readonly ClubReportSnapshotSource.ClubReportSnapshotSourceClient _client;
    public ClubReportSnapshotSourceClient(ClubReportSnapshotSource.ClubReportSnapshotSourceClient client) => _client = client;

    public async Task<ClubSnapshotSourceData> GetAsync(
        Guid clubId, Guid semesterId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        try
        {
            var reply = await _client.GetClubReportDataAsync(Request(clubId, semesterId, start, end),
                cancellationToken: cancellationToken);
            if (!reply.ClubFound) throw new NotFoundException("Club not found.");
            return new ClubSnapshotSourceData(
                reply.ClubName,
                reply.Members.Select(x => new SnapshotMemberData(
                    Guid.Parse(x.Id), Guid.Parse(x.UserId), x.Role, x.Status,
                    DateTime.Parse(x.JoinedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), x.IsActive)).ToList(),
                reply.Events.Select(x => new SnapshotEventData(
                    Guid.Parse(x.Id), x.Title,
                    DateTime.Parse(x.ExpectedDateUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    x.Status, x.IsActive)).ToList());
        }
        catch (RpcException ex)
        {
            throw new ServiceUnavailableException("Club snapshot source is unavailable.", ex);
        }
    }

    internal static ReportSnapshotSourceRequest Request(Guid clubId, Guid semesterId, DateTime start, DateTime end) => new()
    {
        ClubId = clubId.ToString(), SemesterId = semesterId.ToString(),
        SemesterStartUtc = start.ToUniversalTime().ToString("O"),
        SemesterEndUtc = end.ToUniversalTime().ToString("O")
    };
}

public sealed class FinanceReportSnapshotSourceClient : IFinanceReportSnapshotSource
{
    private readonly FinanceReportSnapshotSource.FinanceReportSnapshotSourceClient _client;
    public FinanceReportSnapshotSourceClient(FinanceReportSnapshotSource.FinanceReportSnapshotSourceClient client) => _client = client;

    public async Task<FinanceSnapshotSourceData> GetAsync(
        Guid clubId, Guid semesterId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        try
        {
            var reply = await _client.GetFinanceReportDataAsync(
                ClubReportSnapshotSourceClient.Request(clubId, semesterId, start, end),
                cancellationToken: cancellationToken);
            return new FinanceSnapshotSourceData(
                reply.Items.Select(x => new SnapshotFinanceData(
                    Guid.Parse(x.Id), x.HasActivityId ? Guid.Parse(x.ActivityId) : null, x.Title,
                    DateTime.Parse(x.ProposedDateUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    x.Status, Parse(x.RequestedAmount),
                    x.HasApprovedAmount ? Parse(x.ApprovedAmount) : null,
                    x.HasActualAmount ? Parse(x.ActualAmount) : null)).ToList(),
                reply.HasBalance ? Parse(reply.RemainingBalance) : null);
        }
        catch (RpcException ex)
        {
            throw new ServiceUnavailableException("Finance snapshot source is unavailable.", ex);
        }
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
