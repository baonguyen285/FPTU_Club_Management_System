using Club.Infrastructure.Persistence;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Grpc.ClubAccess.V1;

namespace Club.API.GrpcServices;

// v1 contract. The legacy ClubGrpcServiceImpl remains mapped during caller migration.
public sealed class ClubAccessGrpcServiceImpl : ClubAccessService.ClubAccessServiceBase
{
    private readonly ClubDbContext _db;
    private readonly ILogger<ClubAccessGrpcServiceImpl> _logger;

    public ClubAccessGrpcServiceImpl(ClubDbContext db, ILogger<ClubAccessGrpcServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<ExistenceReply> CheckClubExists(ClubIdRequest request, ServerCallContext context)
    {
        var clubId = ParseId(request.ClubId, "club_id");
        var exists = await _db.Clubs.AnyAsync(x => x.Id == clubId && x.IsActive, context.CancellationToken);
        Log("CheckClubExists", context, exists);
        return new ExistenceReply { Exists = exists };
    }

    public override async Task<ClubSummaryReply> GetClubSummary(ClubIdRequest request, ServerCallContext context)
    {
        var clubId = ParseId(request.ClubId, "club_id");
        var club = await _db.Clubs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clubId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Club was not found."));
        Log("GetClubSummary", context, true);
        return new ClubSummaryReply { ClubId = club.Id.ToString(), Name = club.Name, IsActive = club.IsActive };
    }

    public override async Task<MembershipReply> GetMembership(GetMembershipRequest request, ServerCallContext context)
    {
        var clubId = ParseId(request.ClubId, "club_id");
        var userId = ParseId(request.UserId, "user_id");
        await EnsureClubExistsAsync(clubId, context.CancellationToken);
        var member = await _db.ClubMembers.AsNoTracking().SingleOrDefaultAsync(x => x.ClubId == clubId && x.UserId == userId, context.CancellationToken);
        Log("GetMembership", context, member is not null);
        return member is null
            ? new MembershipReply { Exists = false }
            : new MembershipReply { Exists = true, Status = MapStatus(member.Status), Role = MapRole(member.Role) };
    }

    public override async Task<PermissionReply> CheckClubPermission(CheckClubPermissionRequest request, ServerCallContext context)
    {
        var clubId = ParseId(request.ClubId, "club_id");
        var userId = ParseId(request.UserId, "user_id");
        await EnsureClubExistsAsync(clubId, context.CancellationToken);
        var member = await _db.ClubMembers.AsNoTracking().SingleOrDefaultAsync(x => x.ClubId == clubId && x.UserId == userId && x.Status == Club.Domain.Enums.MembershipStatus.Approved, context.CancellationToken);
        var allowed = member is not null && IsAllowed(member.Role, request.Permission);
        Log("CheckClubPermission", context, allowed);
        return new PermissionReply { IsAllowed = allowed };
    }

    private async Task EnsureClubExistsAsync(Guid clubId, CancellationToken token)
    {
        if (!await _db.Clubs.AnyAsync(x => x.Id == clubId, token))
            throw new RpcException(new Status(StatusCode.NotFound, "Club was not found."));
    }

    private static Guid ParseId(string value, string field) => Guid.TryParse(value, out var id) && id != Guid.Empty
        ? id : throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} must be a non-empty GUID."));

    private static bool IsAllowed(Club.Domain.Enums.ClubRole role, ClubPermission permission) => permission switch
    {
        ClubPermission.ManageFinance => role == Club.Domain.Enums.ClubRole.Treasurer,
        ClubPermission.ManageMembers or ClubPermission.ManageActivities or ClubPermission.SubmitReports => role is Club.Domain.Enums.ClubRole.ClubLeader,
        _ => false
    };

    private static Shared.Kernel.Grpc.ClubAccess.V1.MembershipStatus MapStatus(Club.Domain.Enums.MembershipStatus status) => status switch
    {
        Club.Domain.Enums.MembershipStatus.Approved => Shared.Kernel.Grpc.ClubAccess.V1.MembershipStatus.Approved,
        Club.Domain.Enums.MembershipStatus.Rejected => Shared.Kernel.Grpc.ClubAccess.V1.MembershipStatus.Rejected,
        _ => Shared.Kernel.Grpc.ClubAccess.V1.MembershipStatus.Pending
    };

    private static Shared.Kernel.Grpc.ClubAccess.V1.ClubRole MapRole(Club.Domain.Enums.ClubRole role) => role switch
    {
        Club.Domain.Enums.ClubRole.Treasurer => Shared.Kernel.Grpc.ClubAccess.V1.ClubRole.Treasurer,
        Club.Domain.Enums.ClubRole.ClubLeader => Shared.Kernel.Grpc.ClubAccess.V1.ClubRole.ClubLeader,
        Club.Domain.Enums.ClubRole.LegacyManager => Shared.Kernel.Grpc.ClubAccess.V1.ClubRole.Unspecified,
        _ => Shared.Kernel.Grpc.ClubAccess.V1.ClubRole.Member
    };

    private void Log(string rpc, ServerCallContext context, bool result) => _logger.LogInformation(
        "gRPC {Rpc} completed. Result={Result}; CorrelationId={CorrelationId}", rpc, result,
        context.RequestHeaders.GetValue("x-correlation-id") ?? context.Host);
}
