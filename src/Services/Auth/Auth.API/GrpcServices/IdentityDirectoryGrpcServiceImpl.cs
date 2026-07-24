using Auth.Infrastructure.Persistence;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Grpc.IdentityDirectory.V1;

namespace Auth.API.GrpcServices;

public sealed class IdentityDirectoryGrpcServiceImpl : IdentityDirectoryService.IdentityDirectoryServiceBase
{
    private readonly AuthDbContext _db;
    private readonly ILogger<IdentityDirectoryGrpcServiceImpl> _logger;
    public IdentityDirectoryGrpcServiceImpl(AuthDbContext db, ILogger<IdentityDirectoryGrpcServiceImpl> logger) => (_db, _logger) = (db, logger);

    public override async Task<ListActiveUsersBySystemRoleReply> ListActiveUsersBySystemRole(ListActiveUsersBySystemRoleRequest request, ServerCallContext context)
    {
        var role = request.SystemRole switch
        {
            SystemRole.StudentAffairsAdmin => "StudentAffairsAdmin",
            SystemRole.ClubManager => "ClubManager",
            SystemRole.Student => "Student",
            _ => throw new RpcException(new Status(StatusCode.InvalidArgument, "system_role is required."))
        };

        // Legacy Admin/Advisor are intentionally not returned by the canonical v1 role contract.
        var ids = await _db.Users.AsNoTracking().Where(x => x.IsActive && x.IsEmailVerified && x.Role == role)
            .Select(x => x.Id).ToListAsync(context.CancellationToken);
        var reply = new ListActiveUsersBySystemRoleReply();
        reply.Recipients.AddRange(ids.Select(x => new RecipientIdentity { UserId = x.ToString() }));
        _logger.LogInformation("gRPC ListActiveUsersBySystemRole completed. Role={Role}; RecipientCount={Count}; CorrelationId={CorrelationId}", role, ids.Count, context.RequestHeaders.GetValue("x-correlation-id") ?? context.Host);
        return reply;
    }
}
