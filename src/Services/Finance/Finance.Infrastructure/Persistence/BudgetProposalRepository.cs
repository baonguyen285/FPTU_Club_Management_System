using Finance.Application.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence;

public sealed class BudgetProposalRepository : IBudgetProposalRepository
{
    private readonly FinanceDbContext _context;

    public BudgetProposalRepository(FinanceDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(BudgetProposal proposal, CancellationToken cancellationToken = default) =>
        _context.BudgetProposals.AddAsync(proposal, cancellationToken).AsTask();

    public Task<BudgetProposal?> GetByIdAsync(
        Guid id,
        bool asTracking,
        CancellationToken cancellationToken = default)
    {
        var query = asTracking
            ? _context.BudgetProposals.AsQueryable()
            : _context.BudgetProposals.AsNoTracking();

        return query.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
    }

    public async Task<(IReadOnlyList<BudgetProposal> Items, int TotalItems)> GetAsync(
        Guid? clubId,
        BudgetProposalStatus? status,
        Guid? proposerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BudgetProposals.AsNoTracking().Where(x => x.IsActive);

        if (clubId.HasValue)
        {
            query = query.Where(x => x.ClubId == clubId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (proposerId.HasValue)
        {
            query = query.Where(x => x.ProposerId == proposerId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.ProposedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalItems);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public Task AddTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default) =>
        _context.FinanceTransactions.AddAsync(transaction, cancellationToken).AsTask();

    public Task<bool> TransactionExistsAsync(Guid referenceId, FinanceTransactionType type, CancellationToken cancellationToken = default) =>
        _context.FinanceTransactions.AnyAsync(x => x.ReferenceId == referenceId && x.Type == type && x.IsActive, cancellationToken);

    public async Task<IReadOnlyList<FinanceTransaction>> GetTransactionsAsync(Guid clubId, CancellationToken cancellationToken = default) =>
        await _context.FinanceTransactions.AsNoTracking()
            .Where(x => x.ClubId == clubId && x.IsActive)
            .OrderByDescending(x => x.TransactionDate).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<ClubFinanceBalance?> GetBalanceAsync(Guid clubId, bool asTracking, CancellationToken cancellationToken = default)
    {
        var query = asTracking ? _context.ClubFinanceBalances.AsQueryable() : _context.ClubFinanceBalances.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.ClubId == clubId && x.IsActive, cancellationToken);
    }

    public Task AddBalanceAsync(ClubFinanceBalance balance, CancellationToken cancellationToken = default) =>
        _context.ClubFinanceBalances.AddAsync(balance, cancellationToken).AsTask();
}
