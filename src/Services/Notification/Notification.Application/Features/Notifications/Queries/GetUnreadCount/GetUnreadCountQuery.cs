using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Notification.Application.Interfaces;

namespace Notification.Application.Features.Notifications.Queries.GetUnreadCount
{
    public class GetUnreadCountQuery : IRequest<int>
    {
        public Guid UserId { get; set; }
    }

    public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetUnreadCountQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Notifications.GetUnreadCountAsync(request.UserId);
        }
    }
}
