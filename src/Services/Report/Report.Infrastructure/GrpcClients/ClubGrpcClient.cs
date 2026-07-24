using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Report.Application.Interfaces;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Grpc.ClubAccess.V1;

namespace Report.Infrastructure.GrpcClients;

public sealed class ClubGrpcClient : IClubGrpcClient
{
    private readonly ClubAccessService.ClubAccessServiceClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClubGrpcClient> _logger;

    public ClubGrpcClient(ClubAccessService.ClubAccessServiceClient client, IConfiguration configuration, ILogger<ClubGrpcClient> logger)
        => (_client, _configuration, _logger) = (client, configuration, logger);

    public async Task<bool> CheckClubExistsAsync(Guid clubId, CancellationToken cancellationToken = default)
    {
        var reply = await ExecuteIdempotentAsync(
            "CheckClubExists", "GrpcSettings:ClubAccess:CheckClubExistsTimeoutSeconds",
            (deadline, token) => _client.CheckClubExistsAsync(new ClubIdRequest { ClubId = clubId.ToString() }, deadline: deadline, cancellationToken: token).ResponseAsync,
            cancellationToken);
        return reply.Exists;
    }

    public async Task<bool> CanSubmitReportsAsync(Guid clubId, Guid userId, CancellationToken cancellationToken = default)
    {
        var reply = await ExecuteIdempotentAsync(
            "CheckClubPermission", "GrpcSettings:ClubAccess:CheckClubPermissionTimeoutSeconds",
            (deadline, token) => _client.CheckClubPermissionAsync(new CheckClubPermissionRequest
            {
                ClubId = clubId.ToString(), UserId = userId.ToString(), Permission = ClubPermission.SubmitReports
            }, deadline: deadline, cancellationToken: token).ResponseAsync,
            cancellationToken);
        return reply.IsAllowed;
    }

    // Kept only for the existing review handler while its StudentAffairs authorization is migrated in a later phase.
    public Task<bool> IsClubManagerAsync(Guid clubId, Guid userId, CancellationToken cancellationToken = default)
        => CanSubmitReportsAsync(clubId, userId, cancellationToken);

    private async Task<T> ExecuteIdempotentAsync<T>(string rpc, string timeoutKey, Func<DateTime, CancellationToken, Task<T>> call, CancellationToken token)
    {
        var timeout = _configuration.GetValue(timeoutKey, 3);
        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        var attempts = _configuration.GetValue("GrpcSettings:Retry:IdempotentUnavailableMaxRetries", 1) + 1;
        for (var attempt = 1; ; attempt++)
        {
            try { return await call(deadline, token); }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable && attempt < attempts && DateTime.UtcNow < deadline)
            {
                _logger.LogWarning(ex, "Transient gRPC failure for {Rpc}; retrying attempt {Attempt}.", rpc, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), token);
            }
            catch (RpcException ex) { throw Map(rpc, ex); }
        }
    }

    private static Exception Map(string rpc, RpcException exception) => exception.StatusCode switch
    {
        StatusCode.NotFound => new NotFoundException($"Club dependency did not find the requested resource during {rpc}."),
        StatusCode.PermissionDenied => new ForbiddenException("Club permission was denied."),
        StatusCode.InvalidArgument => new BadRequestException(exception.Status.Detail),
        StatusCode.Unauthenticated => new UnauthorizedException("Internal service authentication failed."),
        StatusCode.DeadlineExceeded or StatusCode.Unavailable => new ServiceUnavailableException($"Club dependency is unavailable during {rpc}.", exception),
        _ => new ServiceUnavailableException($"Club dependency failed during {rpc}.", exception)
    };
}
