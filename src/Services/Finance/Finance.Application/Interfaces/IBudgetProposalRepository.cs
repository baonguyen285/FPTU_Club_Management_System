using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Interfaces;

public interface IBudgetProposalRepository
{
    Task AddAsync(BudgetProposal proposal, CancellationToken cancellationToken = default);
    Task<BudgetProposal?> GetByIdAsync(Guid id, bool asTracking, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<BudgetProposal> Items, int TotalItems)> GetAsync(
        Guid? clubId,
        BudgetProposalStatus? status,
        Guid? proposerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task AddTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> TransactionExistsAsync(Guid referenceId, FinanceTransactionType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceTransaction>> GetTransactionsAsync(Guid clubId, CancellationToken cancellationToken = default);
    Task<ClubFinanceBalance?> GetBalanceAsync(Guid clubId, bool asTracking, CancellationToken cancellationToken = default);
    Task AddBalanceAsync(ClubFinanceBalance balance, CancellationToken cancellationToken = default);
}
