using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Hubs;

namespace Notification.Infrastructure.EventBus
{
    public class RedisEventConsumer : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisEventConsumer> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<NotificationHub> _hubContext;
        private const string ChannelName = "report-events-channel";

        public RedisEventConsumer(
            IConnectionMultiplexer redis, 
            ILogger<RedisEventConsumer> logger,
            IServiceProvider serviceProvider,
            IHubContext<NotificationHub> hubContext)
        {
            _redis = redis;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RedisEventConsumer is starting and subscribing to channel: {Channel}", ChannelName);

            var subscriber = _redis.GetSubscriber();

            await subscriber.SubscribeAsync(ChannelName, async (channel, message) =>
            {
                _logger.LogInformation("Received message from channel {Channel}", channel);
                try
                {
                    var payload = message.ToString();
                    
                    using var doc = JsonDocument.Parse(payload);
                    var root = doc.RootElement;
                    var reportId = root.GetProperty("ReportId").GetGuid();
                    var clubId = root.GetProperty("ClubId").GetGuid();
                    var title = root.GetProperty("Title").GetString();
                    
                    // For demo purposes: the notification should ideally be sent to the Club Manager.
                    // Here, we'll try to find who it should be sent to. If we don't have it in the event, we just broadcast or log.
                    // Assuming the event includes who submitted it, and we want to notify "Admins" or "Managers".
                    // However, we don't know the manager's ID. Let's just assume we send to a specific manager ID for testing.
                    var targetUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

                    var notifMsg = $"A new report '{title}' was submitted for Club ID: {clubId}";

                    // 1. Save to Database
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var notification = new NotificationEntity
                        {
                            UserId = targetUserId,
                            Title = "New Report Submitted",
                            Message = notifMsg,
                            Type = NotificationType.ReportSubmitted,
                            ReferenceId = reportId,
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        
                        await unitOfWork.Notifications.AddAsync(notification);
                        await unitOfWork.SaveChangesAsync(stoppingToken);
                    }

                    // 2. Broadcast via SignalR to the specific user
                    await _hubContext.Clients.Group(targetUserId.ToString())
                        .SendAsync("ReceiveNotification", new 
                        {
                            Title = "New Report Submitted",
                            Message = notifMsg,
                            ReportId = reportId,
                            CreatedAt = DateTime.UtcNow
                        });

                    _logger.LogInformation("Notification saved and broadcasted via SignalR to User {UserId}", targetUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Redis subscription event.");
                }
            });

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            await subscriber.UnsubscribeAsync(ChannelName);
        }
    }
}
