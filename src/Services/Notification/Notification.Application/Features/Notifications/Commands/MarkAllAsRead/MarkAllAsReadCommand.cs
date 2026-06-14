using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Notification.Application.Interfaces;

namespace Notification.Application.Features.Notifications.Commands.MarkAllAsRead
{
    public class MarkAllAsReadCommand : IRequest<bool>
    {
        public Guid UserId { get; set; }
    }

    public class MarkAllAsReadCommandHandler : IRequestHandler<MarkAllAsReadCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public MarkAllAsReadCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(MarkAllAsReadCommand request, CancellationToken cancellationToken)
        {
            var notifications = await _unitOfWork.Notifications.GetByUserIdAsync(request.UserId);

            bool anyUpdated = false;
            foreach (var notification in notifications)
            {
                if (!notification.IsRead)
                {
                    notification.IsRead = true;
                    notification.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Notifications.Update(notification);
                    anyUpdated = true;
                }
            }

            if (anyUpdated)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }
    }
}
