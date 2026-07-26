using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Report.Domain.Entities;
using Report.Domain.Enums;
using Report.Infrastructure.Persistence;
using Shared.Kernel.IntegrationEvents;

namespace Report.API.Jobs;

[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
public sealed class PendingReportReminderJob
{
    public const string RecurringJobId = "pending-report-reminder-v1";
    private readonly ReportDbContext _db;
    private readonly ILogger<PendingReportReminderJob> _logger;

    public PendingReportReminderJob(ReportDbContext db, ILogger<PendingReportReminderJob> logger)
        => (_db, _logger) = (db, logger);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var reminderDate = DateTime.UtcNow.Date;
        var pendingReports = await _db.Reports.AsNoTracking()
            .Where(x => x.Status == ReportStatus.Pending)
            .Select(x => new { x.Id, x.ClubId, x.CreatedBy })
            .ToListAsync(cancellationToken);

        foreach (var report in pendingReports)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var inserted = await _db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (
    SELECT 1 FROM ReportReminderDispatches WITH (UPDLOCK, HOLDLOCK)
    WHERE ReportId = {report.Id} AND ReminderDateUtc = {reminderDate})
BEGIN
    INSERT INTO ReportReminderDispatches (ReportId, ReminderDateUtc, CreatedAtUtc)
    VALUES ({report.Id}, {reminderDate}, {DateTime.UtcNow});
END", cancellationToken);

            if (inserted > 0)
            {
                var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
                var envelope = new IntegrationEventEnvelopeV1(
                    Guid.NewGuid(),
                    "ReportReminderDueV1",
                    "v1",
                    DateTime.UtcNow,
                    "report-service",
                    correlationId,
                    new ReportReminderDueV1(report.Id, report.ClubId, report.CreatedBy, reminderDate));

                _db.OutboxMessages.Add(new OutboxMessage(
                    envelope.EventType,
                    JsonSerializer.Serialize(envelope),
                    legacyPayload: null,
                    envelope.OccurredAtUtc));
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation(
                    "Pending report reminder queued. ReportId={ReportId}; EventId={EventId}; CorrelationId={CorrelationId}",
                    report.Id, envelope.EventId, correlationId);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }
    }
}
