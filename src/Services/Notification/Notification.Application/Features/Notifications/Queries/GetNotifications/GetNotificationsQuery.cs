using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Notification.Application.DTOs;
using Notification.Application.Interfaces;

namespace Notification.Application.Features.Notifications.Queries.GetNotifications
{
    public class GetNotificationsQuery : IRequest<IEnumerable<NotificationDto>>
    {
        public Guid UserId { get; set; }
    }

    public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, IEnumerable<NotificationDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetNotificationsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
        {
            var notifications = await _unitOfWork.Notifications.GetByUserIdAsync(request.UserId);
            return _mapper.Map<IEnumerable<NotificationDto>>(notifications.OrderByDescending(n => n.CreatedAt));
        }
    }
}
