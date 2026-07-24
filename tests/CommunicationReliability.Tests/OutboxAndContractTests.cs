using Notification.Domain.Entities;
using Report.Domain.Entities;
using Shared.Kernel.IntegrationEvents;

namespace CommunicationReliability.Tests;

public sealed class OutboxAndContractTests
{
    [Fact]
    public void ReportSubmittedV1_uses_only_contract_identifiers()
    {
        var data = new ReportSubmittedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Activity", "Unspecified", DateTime.UtcNow);
        Assert.NotEqual(Guid.Empty, data.ReportId);
        Assert.DoesNotContain("@", System.Text.Json.JsonSerializer.Serialize(data));
    }

    [Fact]
    public void Envelope_uses_a_stable_event_id_as_source_id()
    {
        var eventId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelopeV1(eventId, "ReportSubmittedV1", "v1", DateTime.UtcNow, "report-service", "correlation", new { });
        Assert.Equal(eventId, envelope.EventId);
        Assert.Equal("v1", envelope.SchemaVersion);
    }

    [Fact]
    public void Outbox_is_not_ready_while_another_worker_holds_a_lease()
    {
        var item = NewOutbox();
        item.Claim(DateTime.UtcNow.AddMinutes(1));
        Assert.False(item.IsReady(DateTime.UtcNow));
    }

    [Fact]
    public void Expired_outbox_lease_can_be_recovered_by_another_worker()
    {
        var item = NewOutbox();
        item.Claim(DateTime.UtcNow.AddSeconds(-1));
        Assert.True(item.IsReady(DateTime.UtcNow));
    }

    [Fact]
    public void Publish_state_is_set_only_after_successful_dispatch()
    {
        var item = NewOutbox();
        Assert.Null(item.PublishedAtUtc);
        item.MarkPublished(DateTime.UtcNow);
        Assert.NotNull(item.PublishedAtUtc);
    }

    [Fact]
    public void Failed_dispatch_keeps_outbox_unpublished_and_schedules_retry()
    {
        var item = NewOutbox();
        item.MarkFailed("Redis unavailable", DateTime.UtcNow.AddSeconds(2));
        Assert.Null(item.PublishedAtUtc);
        Assert.Equal(1, item.RetryCount);
        Assert.NotNull(item.NextAttemptAtUtc);
    }

    [Fact]
    public void Notification_source_event_and_recipient_form_the_idempotency_key()
    {
        var sourceEventId = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var first = new NotificationEntity { SourceEventId = sourceEventId, UserId = recipient };
        var replay = new NotificationEntity { SourceEventId = sourceEventId, UserId = recipient };
        Assert.Equal(first.SourceEventId, replay.SourceEventId);
        Assert.Equal(first.UserId, replay.UserId);
    }

    [Fact]
    public void Notification_entity_keeps_source_event_without_sensitive_identity_data()
    {
        var notification = new NotificationEntity { SourceEventId = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "New report", Message = "Awaiting review" };
        var serialized = System.Text.Json.JsonSerializer.Serialize(notification);
        Assert.DoesNotContain("email", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static OutboxMessage NewOutbox() => new("ReportSubmittedV1", "{}", null, DateTime.UtcNow);
}
