namespace Notification.Infrastructure.GrpcClients;

public interface IIdentityDirectoryClient
{
    Task<IReadOnlyCollection<Guid>> ListActiveUsersBySystemRoleAsync(string systemRole, CancellationToken cancellationToken = default);
}
