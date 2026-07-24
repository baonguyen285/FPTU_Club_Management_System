using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Notification.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Notification.Application.Features.Notifications.Commands.DeleteNotification
{
    public class DeleteNotificationCommand : IRequest<bool>
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }

    public class DeleteNotificationCommandHandler : IRequestHandler<DeleteNotificationCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteNotificationCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAndUserIdAsync(request.Id, request.UserId);

            if (notification == null)
            {
                throw new NotFoundException($"Notification with ID {request.Id} not found or access denied.");
            }

            notification.IsDeleted = true;
            notification.DeletedAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Notifications.Update(notification);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
