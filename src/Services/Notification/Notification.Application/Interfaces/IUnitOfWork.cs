using System.Threading;
using System.Threading.Tasks;

namespace Notification.Application.Interfaces
{
    public interface IUnitOfWork
    {
        INotificationRepository Notifications { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
