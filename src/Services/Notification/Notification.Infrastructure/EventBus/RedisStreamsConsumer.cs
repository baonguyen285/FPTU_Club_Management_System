using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.GrpcClients;
using Notification.Infrastructure.Hubs;
using Notification.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Shared.Kernel.IntegrationEvents;
using StackExchange.Redis;

namespace Notification.Infrastructure.EventBus;

public sealed class RedisStreamsConsumer : BackgroundService
{
    public const string Stream = "fptu.club.events.report.submitted.v1";
    public const string Group = "notification-service.report-submitted.v1";
    public const string Dlq = "fptu.club.events.report.submitted.v1.dlq";
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<RedisStreamsConsumer> _logger;
    private readonly string _consumerName = $"notification-{Environment.MachineName}-{Guid.NewGuid():N}";
    public int ReconnectDelayMs { get; set; } = 5000;

    public RedisStreamsConsumer(IConnectionMultiplexer redis, IServiceScopeFactory scopes, IHubContext<NotificationHub> hub, ILogger<RedisStreamsConsumer> logger)
        => (_redis, _scopes, _hub, _logger) = (redis, scopes, hub, logger);

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var db = _redis.GetDatabase();
        bool isGroupCreated = false;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!isGroupCreated)
                {
                    try
                    {
                        await db.StreamCreateConsumerGroupAsync(Stream, Group, "0-0", createStream: true);
                        isGroupCreated = true;
                        _logger.LogInformation("Redis Stream consumer group created or verified. Stream={Stream}; Group={Group}; Consumer={Consumer}", Stream, Group, _consumerName);
                    }
                    catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
                    {
                        isGroupCreated = true;
                    }
                }

                if (isGroupCreated)
                {
                    await RecoverPendingAsync(db, token);
                    var entries = await db.StreamReadGroupAsync(Stream, Group, _consumerName, ">", count: 10);
                    if (entries.Length == 0)
                    {
                        await Task.Delay(500, token);
                        continue;
                    }
                    foreach (var entry in entries)
                    {
                        await ProcessAsync(db, entry, token);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or RedisException)
            {
                _logger.LogWarning(ex, "Redis connection or timeout error occurred. Retrying connection... Stream={Stream}; Group={Group}; Consumer={Consumer}; RetryDelayMs={RetryDelayMs}", Stream, Group, _consumerName, ReconnectDelayMs);
                try
                {
                    await Task.Delay(ReconnectDelayMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Redis Stream consumer stopped. Stream={Stream}; Group={Group}; Consumer={Consumer}", Stream, Group, _consumerName);
    }

    private async Task RecoverPendingAsync(IDatabase redis, CancellationToken token)
    {
        try
        {
            // JUSTID keeps the command payload small; each claimed entry is re-read from the stream before processing.
            var raw = await redis.ExecuteAsync("XAUTOCLAIM", Stream, Group, _consumerName, 60000, "0-0", "COUNT", 10, "JUSTID");
            var result = (RedisResult[])raw;
            if (result.Length < 2) return;
            foreach (var idResult in (RedisResult[])result[1])
            {
                var id = idResult.ToString();
                var entries = await redis.StreamRangeAsync(Stream, id, id, count: 1);
                if (entries.Length > 0) await ProcessAsync(redis, entries[0], token);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Pending Redis Stream recovery did not complete; entries remain pending for a later retry.");
        }
    }

    private async Task ProcessAsync(IDatabase redis, StreamEntry entry, CancellationToken token)
    {
        try
        {
            var value = entry.Values.FirstOrDefault(x => x.Name == "envelope").Value;
            var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelopeV1>(value!)
                ?? throw new InvalidDataException("Missing event envelope.");
            if (envelope.SchemaVersion != "v1")
                throw new InvalidDataException("Unsupported event contract.");

            using var scope = _scopes.CreateScope();
            IReadOnlyCollection<Guid> recipients;
            Guid reportId;
            string title;
            string message;
            NotificationType notificationType;

            if (envelope.EventType == "ReportSubmittedV1")
            {
                var data = JsonSerializer.Deserialize<ReportSubmittedV1>(JsonSerializer.Serialize(envelope.Data))
                    ?? throw new InvalidDataException("Invalid ReportSubmittedV1 data.");
                reportId = data.ReportId;
                title = "New report submitted";
                message = "A club report is waiting for review.";
                notificationType = NotificationType.ReportSubmitted;
                recipients = await scope.ServiceProvider.GetRequiredService<IIdentityDirectoryClient>()
                    .ListActiveUsersBySystemRoleAsync("StudentAffairsAdmin", token);
                if (recipients.Count == 0)
                    _logger.LogWarning("No recipients resolved for report submitted event. EventId={EventId}; Role={Role}", envelope.EventId, "StudentAffairsAdmin");
            }
            else if (envelope.EventType == "ReportReminderDueV1")
            {
                var data = JsonSerializer.Deserialize<ReportReminderDueV1>(JsonSerializer.Serialize(envelope.Data))
                    ?? throw new InvalidDataException("Invalid ReportReminderDueV1 data.");
                reportId = data.ReportId;
                title = "Pending report reminder";
                message = "Your club report is still pending review.";
                notificationType = NotificationType.ReportReminderDue;
                recipients = new[] { data.ReporterId };
            }
            else
            {
                throw new InvalidDataException($"Unsupported event type '{envelope.EventType}'.");
            }

            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync(token);
            var createdNotifications = new System.Collections.Generic.List<NotificationEntity>();
            foreach (var recipient in recipients.Distinct())
            {
                var notif = new NotificationEntity
                {
                    Id = Guid.NewGuid(), UserId = recipient, SourceEventId = envelope.EventId,
                    Title = title, Message = message,
                    Type = notificationType, ReferenceId = reportId,
                    TargetUrl = $"/reports/{reportId}", IsRead = false, CreatedAt = DateTime.UtcNow
                };
                createdNotifications.Add(notif);
                context.Notifications.Add(notif);
            }
            try { await context.SaveChangesAsync(token); await transaction.CommitAsync(token); }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync(token);
                _logger.LogInformation("Duplicate notification event ignored. EventId={EventId}", envelope.EventId);
            }

            foreach (var notif in createdNotifications)
            {
                try
                {
                    await _hub.Clients.Group(notif.UserId.ToString()).SendAsync("ReceiveNotification", new
                    {
                        id = notif.Id,
                        userId = notif.UserId,
                        sourceEventId = notif.SourceEventId,
                        reportId,
                        title = notif.Title,
                        message = notif.Message,
                        type = notif.Type.ToString(),
                        targetUrl = notif.TargetUrl,
                        isRead = notif.IsRead,
                        createdAt = notif.CreatedAt
                    }, token);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR push failed after notification persistence. Recipient={Recipient}", notif.UserId); }
            }
            await redis.StreamAcknowledgeAsync(Stream, Group, entry.Id);
        }
        catch (ServiceUnavailableException ex)
        {
            _logger.LogWarning(ex, "Recipient lookup unavailable; event remains pending. EntryId={EntryId}", entry.Id);
        }
        catch (Exception ex)
        {
            await RecordFailureOrDlqAsync(redis, entry, ex, token);
        }
    }

    private async Task RecordFailureOrDlqAsync(IDatabase redis, StreamEntry entry, Exception exception, CancellationToken token)
    {
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        string entryIdStr = entry.Id.ToString();
        var row = await context.StreamProcessingFailures.SingleOrDefaultAsync(x => x.StreamEntryId == entryIdStr, token);
        if (row is null) { row = new StreamProcessingFailure { Id = Guid.NewGuid(), StreamEntryId = entryIdStr, RetryCount = 0 }; context.StreamProcessingFailures.Add(row); }
        row.RetryCount++;
        row.LastErrorCode = exception.GetType().Name;
        row.LastErrorMessage = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
        row.LastFailedAtUtc = DateTime.UtcNow;
        var maxRetries = 5;
        if (row.RetryCount >= maxRetries)
        {
            var envelope = entry.Values.FirstOrDefault(x => x.Name == "envelope").Value;
            await redis.StreamAddAsync(Dlq, new[] { new NameValueEntry("originalStreamId", entry.Id), new NameValueEntry("envelope", envelope), new NameValueEntry("errorCode", row.LastErrorCode), new NameValueEntry("retryCount", row.RetryCount), new NameValueEntry("failedAtUtc", row.LastFailedAtUtc.ToString("O")) });
            context.StreamProcessingFailures.Remove(row);
            await context.SaveChangesAsync(token);
            await redis.StreamAcknowledgeAsync(Stream, Group, entry.Id); // only after DLQ write and DB commit
            return;
        }
        await context.SaveChangesAsync(token);
        _logger.LogWarning("Stream entry failed and remains pending. EntryId={EntryId}; Retry={Retry}", entry.Id, row.RetryCount);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;
}
