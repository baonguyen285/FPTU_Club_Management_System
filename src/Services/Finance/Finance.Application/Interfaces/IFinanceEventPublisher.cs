using Finance.Domain.Entities;

namespace Finance.Application.Interfaces;

public interface IFinanceEventPublisher
{
    Task PublishBudgetAsync(string eventType, BudgetProposal proposal, CancellationToken cancellationToken = default);
}
