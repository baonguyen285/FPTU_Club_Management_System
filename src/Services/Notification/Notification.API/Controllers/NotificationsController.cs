using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Features.Notifications.Commands.MarkAllAsRead;
using Notification.Application.Features.Notifications.Commands.MarkAsRead;
using Notification.Application.Features.Notifications.Queries.GetNotifications;
using Shared.Kernel.Exceptions;

namespace Notification.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public NotificationsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedException("User not authenticated properly.");

            var query = new GetNotificationsQuery { UserId = userId };
            var result = await _mediator.Send(query);
            return Ok(new { success = true, data = result });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedException("User not authenticated properly.");

            var command = new MarkAsReadCommand { Id = id, UserId = userId };
            await _mediator.Send(command);
            return Ok(new { success = true, message = "Notification marked as read." });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedException("User not authenticated properly.");

            var command = new MarkAllAsReadCommand { UserId = userId };
            await _mediator.Send(command);
            return Ok(new { success = true, message = "All notifications marked as read." });
        }
    }
}
