using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Persistence.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly NotificationDbContext _context;

        public NotificationRepository(NotificationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(NotificationEntity notification)
        {
            await _context.Notifications.AddAsync(notification);
        }

        public async Task<NotificationEntity?> GetByIdAsync(Guid id)
        {
            return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
        }

        public async Task<NotificationEntity?> GetByIdAndUserIdAsync(Guid id, Guid userId)
        {
            return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && !n.IsDeleted);
        }

        public async Task<IEnumerable<NotificationEntity>> GetByUserIdAsync(Guid userId, bool? isRead = null)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId && !n.IsDeleted);

            if (isRead.HasValue)
            {
                query = query.Where(n => n.IsRead == isRead.Value);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(Guid userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDeleted);
        }

        public void Update(NotificationEntity notification)
        {
            _context.Notifications.Update(notification);
        }
    }
}
