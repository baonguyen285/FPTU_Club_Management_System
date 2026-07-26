using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Hubs;
using Notification.Infrastructure.Persistence;
using Shared.Kernel.IntegrationEvents;
using StackExchange.Redis;

namespace Notification.Infrastructure.EventBus;

public sealed class ClubWorkflowRedisConsumer : BackgroundService
{
    public const string Stream = "fptu.club.events.workflow.v1";
    public const string Group = "notification-service.club-workflow.v1";
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<ClubWorkflowRedisConsumer> _logger;
    private readonly string _consumer = $"club-workflow-{Environment.MachineName}-{Guid.NewGuid():N}";

    public ClubWorkflowRedisConsumer(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopes,
        IHubContext<NotificationHub> hub,
        ILogger<ClubWorkflowRedisConsumer> logger) =>
        (_redis, _scopes, _hub, _logger) = (redis, scopes, hub, logger);

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var redis = _redis.GetDatabase();
        try
        {
            await redis.StreamCreateConsumerGroupAsync(Stream, Group, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase)) { }

        while (!token.IsCancellationRequested)
        {
            try
            {
                var entries = await redis.StreamReadGroupAsync(Stream, Group, _consumer, ">", count: 10);
                if (entries.Length == 0) { await Task.Delay(500, token); continue; }
                foreach (var entry in entries) await ProcessAsync(redis, entry, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Club workflow notification consumer will retry.");
                await Task.Delay(3000, token);
            }
        }
    }

    private async Task ProcessAsync(IDatabase redis, StreamEntry entry, CancellationToken token)
    {
        var raw = entry.Values.FirstOrDefault(x => x.Name == "envelope").Value;
        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelopeV1>(raw!)
            ?? throw new InvalidDataException("Missing club workflow envelope.");
        if (envelope.EventType != nameof(ClubApplicationReviewedV1))
            throw new InvalidDataException($"Unsupported club workflow event '{envelope.EventType}'.");
        var data = JsonSerializer.Deserialize<ClubApplicationReviewedV1>(
            JsonSerializer.Serialize(envelope.Data))
            ?? throw new InvalidDataException("Invalid ClubApplicationReviewedV1.");

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = data.ApplicantUserId,
            SourceEventId = envelope.EventId,
            Title = data.Status == "Approved" ? "Club application approved" : "Club application rejected",
            Message = data.Status == "Approved"
                ? $"Your application for {data.ProposedClubName} was approved."
                : data.ReviewFeedback ?? $"Your application for {data.ProposedClubName} was rejected.",
            Type = NotificationType.SystemAlert,
            ReferenceId = data.ClubId ?? data.ApplicationId,
            TargetUrl = data.ClubId.HasValue ? $"/clubs/{data.ClubId}" : "/club-applications",
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.Notifications.Add(notification);
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            await redis.StreamAcknowledgeAsync(Stream, Group, entry.Id);
            return;
        }
        await redis.StreamAcknowledgeAsync(Stream, Group, entry.Id);
        try
        {
            await _hub.Clients.Group(notification.UserId.ToString())
                .SendAsync("ReceiveNotification", notification, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Club workflow notification persisted; realtime delivery failed.");
        }
    }
}
