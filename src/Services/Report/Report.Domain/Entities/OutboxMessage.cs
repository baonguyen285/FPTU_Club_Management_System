using Shared.Kernel.Domain;

namespace Report.Domain.Entities;

public sealed class OutboxMessage : BaseEntity
{
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string? LegacyPayload { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private OutboxMessage() { }

    public OutboxMessage(string eventType, string payload, string? legacyPayload, DateTime occurredAtUtc)
    {
        Id = Guid.NewGuid();
        EventType = eventType;
        Payload = payload;
        LegacyPayload = legacyPayload;
        OccurredAtUtc = occurredAtUtc;
    }

    public bool IsReady(DateTime now) => PublishedAtUtc is null && (NextAttemptAtUtc is null || NextAttemptAtUtc <= now) && (LockedUntilUtc is null || LockedUntilUtc <= now);
    public void Claim(DateTime leaseUntilUtc) => LockedUntilUtc = leaseUntilUtc;
    public void MarkPublished(DateTime now) { PublishedAtUtc = now; LockedUntilUtc = null; LastError = null; }
    public void MarkFailed(string error, DateTime nextAttemptAtUtc) { RetryCount++; LastError = error; NextAttemptAtUtc = nextAttemptAtUtc; LockedUntilUtc = null; }
}
