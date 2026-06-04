using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Notification.Infrastructure.EventBus
{
    public class RedisEventConsumer : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisEventConsumer> _logger;
        private const string ChannelName = "report-events-channel";

        public RedisEventConsumer(IConnectionMultiplexer redis, ILogger<RedisEventConsumer> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RedisEventConsumer is starting and subscribing to channel: {Channel}", ChannelName);

            var subscriber = _redis.GetSubscriber();

            await subscriber.SubscribeAsync(ChannelName, (channel, message) =>
            {
                _logger.LogInformation("Received message from channel {Channel}", channel);
                try
                {
                    var payload = message.ToString();
                    _logger.LogInformation("Message raw payload: {Payload}", payload);

                    // De-serialize event to verify structure
                    using var doc = JsonDocument.Parse(payload);
                    var root = doc.RootElement;
                    var reportId = root.GetProperty("ReportId").GetGuid();
                    var clubId = root.GetProperty("ClubId").GetGuid();
                    var title = root.GetProperty("Title").GetString();
                    var submittedBy = root.GetProperty("SubmittedBy").GetGuid();

                    _logger.LogInformation("--- NOTIFICATION TRIGGERED ---");
                    _logger.LogInformation("Report '{Title}' (ID: {ReportId}) submitted for Club ID: {ClubId}.", title, reportId, clubId);
                    _logger.LogInformation("Submitted By User ID: {UserId}.", submittedBy);
                    _logger.LogInformation("------------------------------");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Redis subscription event.");
                }
            });

            // Keep background service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogInformation("RedisEventConsumer is stopping, unsubscribing from channel: {Channel}", ChannelName);
            await subscriber.UnsubscribeAsync(ChannelName);
        }
    }
}
