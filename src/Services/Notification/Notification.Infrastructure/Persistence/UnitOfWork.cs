using System.Threading;
using System.Threading.Tasks;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Persistence.Repositories;

namespace Notification.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly NotificationDbContext _context;

        public UnitOfWork(NotificationDbContext context)
        {
            _context = context;
            Notifications = new NotificationRepository(_context);
        }

        public INotificationRepository Notifications { get; }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
