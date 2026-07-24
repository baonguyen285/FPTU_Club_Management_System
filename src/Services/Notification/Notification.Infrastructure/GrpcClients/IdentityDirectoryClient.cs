using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Grpc.IdentityDirectory.V1;

namespace Notification.Infrastructure.GrpcClients;

public sealed class IdentityDirectoryClient : IIdentityDirectoryClient
{
    private readonly IdentityDirectoryService.IdentityDirectoryServiceClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentityDirectoryClient> _logger;
    public IdentityDirectoryClient(IdentityDirectoryService.IdentityDirectoryServiceClient client, IConfiguration configuration, ILogger<IdentityDirectoryClient> logger)
        => (_client, _configuration, _logger) = (client, configuration, logger);

    public async Task<IReadOnlyCollection<Guid>> ListActiveUsersBySystemRoleAsync(string systemRole, CancellationToken cancellationToken = default)
    {
        var role = systemRole switch
        {
            "StudentAffairsAdmin" => SystemRole.StudentAffairsAdmin,
            "ClubManager" => SystemRole.ClubManager,
            "Student" => SystemRole.Student,
            _ => throw new BadRequestException("Unsupported canonical system role.")
        };
        var deadline = DateTime.UtcNow.AddSeconds(_configuration.GetValue("GrpcSettings:IdentityDirectory:ListActiveUsersBySystemRoleTimeoutSeconds", 3));
        var attempts = _configuration.GetValue("GrpcSettings:Retry:IdempotentUnavailableMaxRetries", 1) + 1;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var reply = await _client.ListActiveUsersBySystemRoleAsync(new ListActiveUsersBySystemRoleRequest { SystemRole = role }, deadline: deadline, cancellationToken: cancellationToken).ResponseAsync;
                return reply.Recipients.Select(x => Guid.TryParse(x.UserId, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).Distinct().ToArray();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable && attempt < attempts && DateTime.UtcNow < deadline)
            {
                _logger.LogWarning(ex, "Identity Directory is temporarily unavailable; retrying role lookup.");
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
            {
                throw new ServiceUnavailableException("Identity Directory is unavailable.", ex);
            }
            catch (RpcException ex) { throw new BadRequestException($"Identity Directory request failed: {ex.StatusCode}."); }
        }
    }
}
