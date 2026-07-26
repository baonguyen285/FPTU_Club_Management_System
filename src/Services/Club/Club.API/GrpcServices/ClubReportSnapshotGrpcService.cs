using System.Globalization;
using Club.Infrastructure.Persistence;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Grpc.SmartReports.V1;

namespace Club.API.GrpcServices;

public sealed class ClubReportSnapshotGrpcService : ClubReportSnapshotSource.ClubReportSnapshotSourceBase
{
    private readonly ClubDbContext _db;

    public ClubReportSnapshotGrpcService(ClubDbContext db) => _db = db;

    public override async Task<ClubReportDataReply> GetClubReportData(
        ReportSnapshotSourceRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ClubId, out var clubId)
            || !DateTime.TryParse(request.SemesterStartUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start)
            || !DateTime.TryParse(request.SemesterEndUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var end))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid snapshot source request."));

        var club = await _db.Clubs.AsNoTracking()
            .Where(x => x.Id == clubId && x.IsActive)
            .Select(x => new { x.Name })
            .SingleOrDefaultAsync(context.CancellationToken);

        if (club is null) return new ClubReportDataReply { ClubFound = false };

        var members = await _db.ClubMembers.AsNoTracking()
            .Where(x => x.ClubId == clubId && x.JoinedAt <= end)
            .Select(x => new { x.Id, x.UserId, x.Role, x.Status, x.JoinedAt, x.IsActive })
            .ToListAsync(context.CancellationToken);
        var events = await _db.Events.AsNoTracking()
            .Where(x => x.ClubId == clubId && x.ExpectedDate >= start && x.ExpectedDate <= end)
            .Select(x => new { x.Id, x.Title, x.ExpectedDate, x.Status, x.IsActive })
            .ToListAsync(context.CancellationToken);

        var reply = new ClubReportDataReply { ClubFound = true, ClubName = club.Name };
        reply.Members.AddRange(members.Select(x => new SnapshotMember
        {
            Id = x.Id.ToString(), UserId = x.UserId.ToString(), Role = (int)x.Role,
            Status = (int)x.Status, JoinedAtUtc = x.JoinedAt.ToUniversalTime().ToString("O"), IsActive = x.IsActive
        }));
        reply.Events.AddRange(events.Select(x => new SnapshotEvent
        {
            Id = x.Id.ToString(), Title = x.Title, ExpectedDateUtc = x.ExpectedDate.ToUniversalTime().ToString("O"),
            Status = (int)x.Status, IsActive = x.IsActive
        }));
        return reply;
    }
}
