using System.Threading.Tasks;
using Notification.Domain.Entities;

namespace Notification.Application.Interfaces
{
    public interface INotificationRepository
    {
        Task AddAsync(NotificationEntity notification);
        Task<System.Collections.Generic.IEnumerable<NotificationEntity>> GetByUserIdAsync(System.Guid userId, bool? isRead = null);
        Task<NotificationEntity?> GetByIdAndUserIdAsync(System.Guid id, System.Guid userId);
        Task<int> GetUnreadCountAsync(System.Guid userId);
        Task<NotificationEntity?> GetByIdAsync(System.Guid id);
        void Update(NotificationEntity notification);
    }
}
