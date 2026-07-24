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
    public class GetNotificationsQuery : IRequest<(IEnumerable<NotificationDto> Items, int TotalItems, int UnreadCount)>
    {
        public Guid UserId { get; set; }
        public bool? IsRead { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, (IEnumerable<NotificationDto> Items, int TotalItems, int UnreadCount)>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetNotificationsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<(IEnumerable<NotificationDto> Items, int TotalItems, int UnreadCount)> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
        {
            var allNotifications = (await _unitOfWork.Notifications.GetByUserIdAsync(request.UserId, request.IsRead)).ToList();
            var totalItems = allNotifications.Count;
            var unreadCount = await _unitOfWork.Notifications.GetUnreadCountAsync(request.UserId);

            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

            var pagedNotifications = allNotifications
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            var dtos = _mapper.Map<IEnumerable<NotificationDto>>(pagedNotifications);
            return (dtos, totalItems, unreadCount);
        }
    }
}
