using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Report.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Report.Infrastructure.Messaging;

public sealed class OutboxDispatcher : BackgroundService
{
    public const string ReportSubmittedStream = "fptu.club.events.report.submitted.v1";
    private const string LegacyChannel = "report-events-channel";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<OutboxDispatcher> logger)
        => (_scopeFactory, _configuration, _logger) = (scopeFactory, configuration, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_configuration.GetValue("Outbox:PollIntervalSeconds", 2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Outbox dispatch loop failed."); }
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IRedisStreamProducer>();
        var now = DateTime.UtcNow;
        var batchSize = _configuration.GetValue("Outbox:BatchSize", 20);
        var messages = await db.OutboxMessages.Where(x => x.PublishedAtUtc == null && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) && (x.LockedUntilUtc == null || x.LockedUntilUtc <= now))
            .OrderBy(x => x.OccurredAtUtc).Take(batchSize).ToListAsync(token);

        foreach (var message in messages)
        {
            try
            {
                message.Claim(now.AddSeconds(_configuration.GetValue("Outbox:LeaseSeconds", 30)));
                await db.SaveChangesAsync(token); // optimistic RowVersion claim prevents concurrent workers from publishing twice.
            }
            catch (DbUpdateConcurrencyException) { db.Entry(message).State = EntityState.Detached; continue; }

            try
            {
                await producer.AddAsync(ReportSubmittedStream, "envelope", message.Payload, token);
                message.MarkPublished(DateTime.UtcNow);
                await db.SaveChangesAsync(token);

                // Temporary bridge retained until CR-3 consumes the Redis Stream. It is best-effort and does not affect durable publication.
                if (_configuration.GetValue("Messaging:LegacyPubSubBridgeEnabled", true) && !string.IsNullOrWhiteSpace(message.LegacyPayload))
                    await scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetSubscriber().PublishAsync(LegacyChannel, message.LegacyPayload);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var retry = Math.Min(60, Math.Pow(2, message.RetryCount + 1));
                message.MarkFailed(ex.Message, DateTime.UtcNow.AddSeconds(retry));
                await db.SaveChangesAsync(token);
                _logger.LogWarning(ex, "Outbox message {OutboxId} dispatch failed; it will retry.", message.Id);
            }
        }
    }
}
