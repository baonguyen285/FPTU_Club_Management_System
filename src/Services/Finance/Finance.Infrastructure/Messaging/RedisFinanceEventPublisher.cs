using System.Text.Json;
using Finance.Application.Interfaces;
using Finance.Domain.Entities;
using Shared.Kernel.IntegrationEvents;
using StackExchange.Redis;

namespace Finance.Infrastructure.Messaging;

public sealed class RedisFinanceEventPublisher : IFinanceEventPublisher
{
    public const string StreamName = "fptu.club.events.finance.budget.v1";
    private readonly IConnectionMultiplexer _redis;

    public RedisFinanceEventPublisher(IConnectionMultiplexer redis) => _redis = redis;

    public async Task PublishBudgetAsync(string eventType, BudgetProposal proposal, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var occurredAt = DateTime.UtcNow;
        var envelope = new IntegrationEventEnvelopeV1(
            Guid.NewGuid(), eventType, "v1", occurredAt, "finance-service", Guid.NewGuid().ToString("N"),
            new BudgetWorkflowEventV1(
                proposal.Id, proposal.ClubId, proposal.RequestedAmount, proposal.ApprovedAmount ?? 0,
                proposal.ActualAmount, proposal.Status.ToString(), occurredAt));
        await _redis.GetDatabase().StreamAddAsync(
            StreamName, "envelope", JsonSerializer.Serialize(envelope));
    }
}
