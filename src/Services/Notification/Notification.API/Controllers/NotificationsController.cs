using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Notification.Application.Features.Notifications.Commands.DeleteNotification;
using Notification.Application.Features.Notifications.Commands.MarkAllAsRead;
using Notification.Application.Features.Notifications.Commands.MarkAsRead;
using Notification.Application.Features.Notifications.Queries.GetNotifications;
using Notification.Application.Features.Notifications.Queries.GetUnreadCount;
using Notification.Infrastructure.Hubs;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.GrpcClients;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;
using Shared.Kernel.Security;
using System.Linq;

namespace Notification.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly NotificationDbContext _db;
        private readonly IIdentityDirectoryClient _identityDirectory;

        public NotificationsController(IMediator mediator, IHubContext<NotificationHub> hubContext, NotificationDbContext db, IIdentityDirectoryClient identityDirectory)
        {
            _mediator = mediator;
            _hubContext = hubContext;
            _db = db;
            _identityDirectory = identityDirectory;
        }

        private Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedException("User not authenticated properly.");

            return userId;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool? isRead, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserId();
            var query = new GetNotificationsQuery
            {
                UserId = userId,
                IsRead = isRead,
                Page = page,
                PageSize = pageSize
            };

            var (items, totalItems, unreadCount) = await _mediator.Send(query);
            var safePageSize = pageSize <= 0 ? 20 : pageSize;
            var totalPages = (int)Math.Ceiling((double)totalItems / safePageSize);

            var meta = new
            {
                page = page <= 0 ? 1 : page,
                pageSize = safePageSize,
                totalItems,
                totalPages,
                unreadCount
            };

            return Ok(new ApiResponse<object>(items, "Fetched notifications successfully.", meta));
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            var count = await _mediator.Send(new GetUnreadCountQuery { UserId = userId });
            return Ok(new ApiResponse<object>(new { unreadCount = count }, "Fetched unread count successfully."));
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userId = GetCurrentUserId();
            var command = new MarkAsReadCommand { Id = id, UserId = userId };
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Notification marked as read."));
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            var command = new MarkAllAsReadCommand { UserId = userId };
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "All notifications marked as read."));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(Guid id)
        {
            var userId = GetCurrentUserId();
            var command = new DeleteNotificationCommand { Id = id, UserId = userId };
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Notification soft-deleted successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPost("broadcast")]
        public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationRequest request)
        {
            var sourceEventId = request.SourceEventId ?? Guid.NewGuid();
            var role = request.TargetRole ?? "StudentAffairsAdmin";
            var recipients = await _identityDirectory.ListActiveUsersBySystemRoleAsync(role, HttpContext.RequestAborted);
            await using var transaction = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            var createdNotifications = new System.Collections.Generic.List<NotificationEntity>();

            foreach (var userId in recipients.Distinct())
            {
                var notif = new NotificationEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SourceEventId = sourceEventId,
                    Title = request.Title,
                    Message = request.Message,
                    Type = NotificationType.SystemAlert,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                createdNotifications.Add(notif);
                _db.Notifications.Add(notif);
            }
            try
            {
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
                await transaction.CommitAsync(HttpContext.RequestAborted);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
            {
                await transaction.RollbackAsync(HttpContext.RequestAborted);
            }

            foreach (var notif in createdNotifications)
            {
                try
                {
                    await _hubContext.Clients.Group(notif.UserId.ToString()).SendAsync("ReceiveNotification", new
                    {
                        id = notif.Id,
                        userId = notif.UserId,
                        sourceEventId = notif.SourceEventId,
                        title = notif.Title,
                        message = notif.Message,
                        type = notif.Type.ToString(),
                        isRead = notif.IsRead,
                        createdAt = notif.CreatedAt
                    }, HttpContext.RequestAborted);
                }
                catch { /* Persisted durable notifications remain available through REST after a push failure. */ }
            }
            return Ok(new ApiResponse<object>(new { sourceEventId, recipientCount = recipients.Count }, "Broadcast persisted and sent."));
        }
    }

    public class BroadcastNotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? TargetRole { get; set; }
        public Guid? SourceEventId { get; set; }
    }
}
