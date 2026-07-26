using Finance.Application.DTOs;
using Finance.Domain.Enums;

namespace Finance.Application.Interfaces;

public interface IBudgetProposalService
{
    Task<BudgetProposalDto> CreateAsync(
        CreateBudgetProposalCommand command,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default);
    Task<PagedResult<BudgetProposalDto>> GetAsync(
        Guid actorId,
        string actorRole,
        Guid? clubId,
        BudgetProposalStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<BudgetProposalDto> GetByIdAsync(
        Guid id,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default);
    Task<BudgetProposalDto> UpdateAsync(
        Guid id,
        UpdateBudgetProposalCommand command,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default);
    Task<BudgetProposalDto> SubmitAsync(
        Guid id,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default);
    Task<BudgetProposalDto> ApproveAsync(
        Guid id,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default);
    Task<BudgetProposalDto> PartiallyApproveAsync(
        Guid id,
        decimal approvedAmount,
        string feedback,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default);
    Task<BudgetProposalDto> RejectAsync(
        Guid id,
        string feedback,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default);
    Task<BudgetProposalDto> SettleAsync(Guid id, SettleBudgetProposalCommand command, Guid actorId, string actorRole, CancellationToken cancellationToken = default);
    Task<ClubFinanceBalanceDto> GetBalanceAsync(Guid clubId, Guid actorId, string actorRole, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceTransactionDto>> GetTransactionsAsync(Guid clubId, Guid actorId, string actorRole, CancellationToken cancellationToken = default);
    Task<FinanceTransactionDto> CreateTransactionAsync(CreateFinanceTransactionCommand command, Guid actorId, string actorRole, CancellationToken cancellationToken = default);
}
