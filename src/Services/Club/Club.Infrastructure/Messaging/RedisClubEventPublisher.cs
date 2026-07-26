using System.Diagnostics;
using System.Text.Json;
using Club.Application.Interfaces;
using Club.Domain.Entities;
using Shared.Kernel.IntegrationEvents;
using StackExchange.Redis;

namespace Club.Infrastructure.Messaging;

public sealed class RedisClubEventPublisher : IClubEventPublisher
{
    public const string ActivityStream = "fptu.club.events.activity.v1";
    public const string WorkflowStream = "fptu.club.events.workflow.v1";
    private readonly IConnectionMultiplexer _redis;

    public RedisClubEventPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishActivityCreatedAsync(Event clubEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var envelope = new IntegrationEventEnvelopeV1(
            Guid.NewGuid(),
            nameof(ActivityCreatedV1),
            "1.0",
            DateTime.UtcNow,
            "club-service",
            Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            new ActivityCreatedV1(
                clubEvent.Id,
                clubEvent.ClubId,
                clubEvent.Title,
                clubEvent.ExpectedDate.ToUniversalTime()));
        await _redis.GetDatabase().StreamAddAsync(
            ActivityStream,
            "envelope",
            JsonSerializer.Serialize(envelope));
    }

    public async Task PublishClubApplicationReviewedAsync(
        ClubApplication application, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var envelope = new IntegrationEventEnvelopeV1(
            Guid.NewGuid(),
            nameof(ClubApplicationReviewedV1),
            "v1",
            DateTime.UtcNow,
            "club-service",
            Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            new ClubApplicationReviewedV1(
                application.Id,
                application.ApplicantUserId,
                application.CreatedClubId,
                application.ProposedClubName,
                application.Status.ToString(),
                application.ReviewFeedback));
        await _redis.GetDatabase().StreamAddAsync(
            WorkflowStream, "envelope", JsonSerializer.Serialize(envelope));
    }
}
