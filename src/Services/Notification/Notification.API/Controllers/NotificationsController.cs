using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Notification.Application.Features.Notifications.Commands.MarkAllAsRead;
using Notification.Application.Features.Notifications.Commands.MarkAsRead;
using Notification.Application.Features.Notifications.Queries.GetNotifications;
using Notification.Infrastructure.Hubs;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;

namespace Notification.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationsController(IMediator mediator, IHubContext<NotificationHub> hubContext)
        {
            _mediator = mediator;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedException("User not authenticated properly.");

            var query = new GetNotificationsQuery { UserId = userId };
            var result = await _mediator.Send(query);
            return Ok(new ApiResponse<object>(result, "Fetched notifications successfully."));
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedException("User not authenticated properly.");

            var command = new MarkAsReadCommand { Id = id, UserId = userId };
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Notification marked as read."));
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedException("User not authenticated properly.");

            var command = new MarkAllAsReadCommand { UserId = userId };
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "All notifications marked as read."));
        }

        [Authorize(Roles = "Admin,Advisor")]
        [HttpPost("broadcast")]
        public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationRequest request)
        {
            var payload = new
            {
                id = Guid.NewGuid(),
                request.Title,
                request.Message,
                type = "SystemAlert",
                targetRole = request.TargetRole,
                isRead = false,
                createdAt = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("ReceiveNotification", payload);

            return Ok(new ApiResponse<object>(payload, "Broadcast sent successfully."));
        }
    }

    public class BroadcastNotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? TargetRole { get; set; }
    }
}
