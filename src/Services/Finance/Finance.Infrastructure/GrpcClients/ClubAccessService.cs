using Finance.Application.Interfaces;
using Grpc.Core;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Grpc.ClubAccess.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ClubAccessGrpc = Shared.Kernel.Grpc.ClubAccess.V1.ClubAccessService;

namespace Finance.Infrastructure.GrpcClients;

public sealed class ClubAccessService : IClubAccessService
{
    private readonly ClubAccessGrpc.ClubAccessServiceClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClubAccessService> _logger;

    public ClubAccessService(ClubAccessGrpc.ClubAccessServiceClient client, IConfiguration configuration, ILogger<ClubAccessService> logger)
        => (_client, _configuration, _logger) = (client, configuration, logger);

    public async Task<bool> ClubExistsAsync(Guid clubId, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteIdempotentAsync("CheckClubExists", "GrpcSettings:ClubAccess:CheckClubExistsTimeoutSeconds",
            (deadline, token) => _client.CheckClubExistsAsync(new ClubIdRequest { ClubId = clubId.ToString() }, deadline: deadline, cancellationToken: token).ResponseAsync, cancellationToken);
        return result.Exists;
    }

    public async Task<bool> CanManageFinanceAsync(Guid clubId, Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteIdempotentAsync("CheckClubPermission", "GrpcSettings:ClubAccess:CheckClubPermissionTimeoutSeconds",
            (deadline, token) => _client.CheckClubPermissionAsync(new CheckClubPermissionRequest { ClubId = clubId.ToString(), UserId = userId.ToString(), Permission = ClubPermission.ManageFinance }, deadline: deadline, cancellationToken: token).ResponseAsync, cancellationToken);
        return result.IsAllowed;
    }

    private async Task<T> ExecuteIdempotentAsync<T>(string rpc, string timeoutKey, Func<DateTime, CancellationToken, Task<T>> call, CancellationToken token)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_configuration.GetValue(timeoutKey, 3));
        var attempts = _configuration.GetValue("GrpcSettings:Retry:IdempotentUnavailableMaxRetries", 1) + 1;
        for (var attempt = 1; ; attempt++)
        {
            try { return await call(deadline, token); }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable && attempt < attempts && DateTime.UtcNow < deadline)
            {
                _logger.LogWarning(ex, "Transient gRPC failure for {Rpc}; retrying attempt {Attempt}.", rpc, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), token);
            }
            catch (RpcException ex) { throw ex.StatusCode switch
            {
                StatusCode.NotFound => new NotFoundException("Club was not found."),
                StatusCode.PermissionDenied => new ForbiddenException("Club permission was denied."),
                StatusCode.InvalidArgument => new BadRequestException(ex.Status.Detail),
                StatusCode.DeadlineExceeded or StatusCode.Unavailable => new ServiceUnavailableException("Club dependency is unavailable.", ex),
                _ => new ServiceUnavailableException("Club dependency failed.", ex)
            }; }
        }
    }
}
