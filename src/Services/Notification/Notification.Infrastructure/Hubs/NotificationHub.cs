using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                // Add the connection to a group named after the user's ID
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                _logger.LogInformation("User {UserId} connected to NotificationHub with ConnectionId {ConnectionId}", userId, Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
                _logger.LogInformation("User {UserId} disconnected from NotificationHub. ConnectionId {ConnectionId}", userId, Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
