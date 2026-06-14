using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Notification.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Notification.Application.Features.Notifications.Commands.MarkAsRead
{
    public class MarkAsReadCommand : IRequest<bool>
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }

    public class MarkAsReadCommandHandler : IRequestHandler<MarkAsReadCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public MarkAsReadCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(MarkAsReadCommand request, CancellationToken cancellationToken)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(request.Id);

            if (notification == null || notification.UserId != request.UserId)
            {
                throw new NotFoundException($"Notification with ID {request.Id} not found or access denied.");
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Notifications.Update(notification);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }
    }
}
