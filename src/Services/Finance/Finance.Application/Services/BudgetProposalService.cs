using Finance.Application.DTOs;
using Finance.Application.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace Finance.Application.Services;

public sealed class BudgetProposalService : IBudgetProposalService
{
    private readonly IBudgetProposalRepository _repository;
    private readonly IClubAccessService _clubAccess;
    private readonly IFinanceEventPublisher _events;

    public BudgetProposalService(
        IBudgetProposalRepository repository,
        IClubAccessService clubAccess,
        IFinanceEventPublisher events)
    {
        _repository = repository;
        _clubAccess = clubAccess;
        _events = events;
    }

    public async Task<BudgetProposalDto> CreateAsync(
        CreateBudgetProposalCommand command,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        await EnsureClubExistsAsync(command.ClubId, cancellationToken);
        await EnsureClubAccessAsync(command.ClubId, actorId, actorRole, cancellationToken);

        var proposal = new BudgetProposal(
            command.ClubId,
            command.ActivityId,
            actorId,
            command.EventName,
            command.RequestedAmount,
            command.BudgetDetailsJson);

        await _repository.AddAsync(proposal, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return Map(proposal);
    }

    public async Task<PagedResult<BudgetProposalDto>> GetAsync(
        Guid actorId,
        string actorRole,
        Guid? clubId,
        BudgetProposalStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        Guid? proposerId = null;
        if (!IsAdmin(actorRole))
        {
            if (clubId.HasValue)
            {
                await EnsureClubAccessAsync(clubId.Value, actorId, actorRole, cancellationToken);
            }
            else
            {
                proposerId = actorId;
            }
        }

        var (items, totalItems) = await _repository.GetAsync(
            clubId,
            status,
            proposerId,
            page,
            pageSize,
            cancellationToken);

        return new PagedResult<BudgetProposalDto>(
            items.Select(Map).ToList(),
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<BudgetProposalDto> GetByIdAsync(
        Guid id,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var proposal = await GetRequiredAsync(id, false, cancellationToken);
        await EnsureCanViewAsync(proposal, actorId, actorRole, cancellationToken);
        return Map(proposal);
    }

    public async Task<BudgetProposalDto> UpdateAsync(
        Guid id,
        UpdateBudgetProposalCommand command,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var proposal = await GetRequiredAsync(id, true, cancellationToken);
        await EnsureCanModifyAsync(proposal, actorId, actorRole, cancellationToken);
        proposal.Update(command.ActivityId, command.EventName, command.RequestedAmount, command.BudgetDetailsJson);
        await _repository.SaveChangesAsync(cancellationToken);
        return Map(proposal);
    }

    public async Task<BudgetProposalDto> SubmitAsync(
        Guid id,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var proposal = await GetRequiredAsync(id, true, cancellationToken);
        await EnsureCanModifyAsync(proposal, actorId, actorRole, cancellationToken);
        proposal.Submit();
        await _repository.SaveChangesAsync(cancellationToken);
        return Map(proposal);
    }

    public async Task<BudgetProposalDto> ApproveAsync(
        Guid id,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(actorRole);
        var proposal = await GetRequiredAsync(id, true, cancellationToken);
        proposal.Approve(actorId);
        await AddDisbursementAsync(proposal, actorId, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        await _events.PublishBudgetAsync("BudgetApprovedV1", proposal, cancellationToken);
        return Map(proposal);
    }

    public async Task<BudgetProposalDto> PartiallyApproveAsync(
        Guid id,
        decimal approvedAmount,
        string feedback,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(actorRole);
        var proposal = await GetRequiredAsync(id, true, cancellationToken);
        proposal.PartiallyApprove(actorId, approvedAmount, feedback);
        await AddDisbursementAsync(proposal, actorId, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        await _events.PublishBudgetAsync("BudgetApprovedV1", proposal, cancellationToken);
        return Map(proposal);
    }

    public async Task<BudgetProposalDto> SettleAsync(
        Guid id, SettleBudgetProposalCommand command, Guid actorId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        var proposal = await GetRequiredAsync(id, true, cancellationToken);
        await EnsureClubAccessAsync(proposal.ClubId, actorId, actorRole, cancellationToken);
        if (await _repository.TransactionExistsAsync(proposal.Id, FinanceTransactionType.Expense, cancellationToken))
            throw new ConflictException("Proposal has already been settled.");

        proposal.Settle(actorId, command.ActualAmount, command.ReceiptUrl, command.Description);
        var balance = await GetOrCreateBalanceAsync(proposal.ClubId, cancellationToken);
        if (balance.AvailableAmount < command.ActualAmount)
            throw new ConflictException("Club balance is insufficient.");
        balance.SpentAmount += command.ActualAmount;
        balance.AvailableAmount = balance.AllocatedAmount - balance.SpentAmount;
        balance.UpdatedAt = DateTime.UtcNow;
        await _repository.AddTransactionAsync(new FinanceTransaction
        {
            Id = Guid.NewGuid(), ClubId = proposal.ClubId, ReferenceId = proposal.Id,
            Amount = command.ActualAmount, Type = FinanceTransactionType.Expense,
            Description = command.Description?.Trim() ?? $"Settlement for {proposal.EventName}",
            ReceiptUrl = command.ReceiptUrl.Trim(), CreatedBy = actorId
        }, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        await _events.PublishBudgetAsync("BudgetSettledV1", proposal, cancellationToken);
        return Map(proposal);
    }

    public async Task<ClubFinanceBalanceDto> GetBalanceAsync(Guid clubId, Guid actorId, string actorRole, CancellationToken cancellationToken = default)
    {
        await EnsureClubExistsAsync(clubId, cancellationToken);
        await EnsureClubAccessAsync(clubId, actorId, actorRole, cancellationToken);
        var balance = await _repository.GetBalanceAsync(clubId, false, cancellationToken);
        return balance == null
            ? new ClubFinanceBalanceDto(clubId, 0, 0, 0, null)
            : new ClubFinanceBalanceDto(clubId, balance.AllocatedAmount, balance.SpentAmount, balance.AvailableAmount, balance.UpdatedAt);
    }

    public async Task<IReadOnlyList<FinanceTransactionDto>> GetTransactionsAsync(Guid clubId, Guid actorId, string actorRole, CancellationToken cancellationToken = default)
    {
        await EnsureClubExistsAsync(clubId, cancellationToken);
        await EnsureClubAccessAsync(clubId, actorId, actorRole, cancellationToken);
        return (await _repository.GetTransactionsAsync(clubId, cancellationToken)).Select(MapTransaction).ToList();
    }

    public async Task<FinanceTransactionDto> CreateTransactionAsync(CreateFinanceTransactionCommand command, Guid actorId, string actorRole, CancellationToken cancellationToken = default)
    {
        EnsureAdmin(actorRole);
        await EnsureClubExistsAsync(command.ClubId, cancellationToken);
        if (command.Amount <= 0) throw new BadRequestException("Amount must be greater than zero.");
        if (command.ReferenceId.HasValue
            && await _repository.TransactionExistsAsync(command.ReferenceId.Value, command.Type, cancellationToken))
            throw new ConflictException("A transaction of this type already exists for the reference.");
        var transaction = new FinanceTransaction
        {
            Id = Guid.NewGuid(), ClubId = command.ClubId, ReferenceId = command.ReferenceId,
            Amount = command.Amount, Type = command.Type, Description = command.Description.Trim(),
            ReceiptUrl = command.ReceiptUrl?.Trim(), CreatedBy = actorId
        };
        var balance = await GetOrCreateBalanceAsync(command.ClubId, cancellationToken);
        if (command.Type is FinanceTransactionType.Allocation or FinanceTransactionType.Disbursement)
            balance.AllocatedAmount += command.Amount;
        else if (command.Type == FinanceTransactionType.Expense)
            balance.SpentAmount += command.Amount;
        balance.AvailableAmount = balance.AllocatedAmount - balance.SpentAmount;
        if (balance.AvailableAmount < 0) throw new ConflictException("Transaction would make the balance negative.");
        balance.UpdatedAt = DateTime.UtcNow;
        await _repository.AddTransactionAsync(transaction, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return MapTransaction(transaction);
    }

    private async Task AddDisbursementAsync(BudgetProposal proposal, Guid actorId, CancellationToken cancellationToken)
    {
        if (await _repository.TransactionExistsAsync(proposal.Id, FinanceTransactionType.Disbursement, cancellationToken))
            throw new ConflictException("Proposal disbursement already exists.");
        var amount = proposal.ApprovedAmount!.Value;
        var balance = await GetOrCreateBalanceAsync(proposal.ClubId, cancellationToken);
        balance.AllocatedAmount += amount;
        balance.AvailableAmount = balance.AllocatedAmount - balance.SpentAmount;
        balance.UpdatedAt = DateTime.UtcNow;
        await _repository.AddTransactionAsync(new FinanceTransaction
        {
            Id = Guid.NewGuid(), ClubId = proposal.ClubId, ReferenceId = proposal.Id,
            Amount = amount, Type = FinanceTransactionType.Disbursement,
            Description = $"Approved budget for {proposal.EventName}", CreatedBy = actorId
        }, cancellationToken);
    }

    private async Task<ClubFinanceBalance> GetOrCreateBalanceAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var balance = await _repository.GetBalanceAsync(clubId, true, cancellationToken);
        if (balance != null) return balance;
        balance = new ClubFinanceBalance { Id = Guid.NewGuid(), ClubId = clubId };
        await _repository.AddBalanceAsync(balance, cancellationToken);
        return balance;
    }

    public async Task<BudgetProposalDto> RejectAsync(
        Guid id,
        string feedback,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(actorRole);
        var proposal = await GetRequiredAsync(id, true, cancellationToken);
        proposal.Reject(actorId, feedback);
        await _repository.SaveChangesAsync(cancellationToken);
        return Map(proposal);
    }

    private async Task<BudgetProposal> GetRequiredAsync(
        Guid id,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(id, asTracking, cancellationToken)
            ?? throw new NotFoundException("Budget proposal not found.");
    }

    private async Task EnsureClubExistsAsync(Guid clubId, CancellationToken cancellationToken)
    {
        if (!await _clubAccess.ClubExistsAsync(clubId, cancellationToken))
        {
            throw new NotFoundException("Club not found or inactive.");
        }
    }

    private async Task EnsureClubAccessAsync(
        Guid clubId,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(actorRole))
        {
            return;
        }

        if (!await _clubAccess.CanManageFinanceAsync(clubId, actorId, cancellationToken))
        {
            throw new ForbiddenException("You do not have finance permission for this club.");
        }
    }

    private async Task EnsureCanViewAsync(
        BudgetProposal proposal,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(actorRole) || proposal.ProposerId == actorId)
        {
            return;
        }

        await EnsureClubAccessAsync(proposal.ClubId, actorId, actorRole, cancellationToken);
    }

    private async Task EnsureCanModifyAsync(
        BudgetProposal proposal,
        Guid actorId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(actorRole))
        {
            return;
        }

        if (proposal.ProposerId != actorId)
        {
            throw new ForbiddenException("Only the proposal owner can modify or submit it.");
        }

        await EnsureClubAccessAsync(proposal.ClubId, actorId, actorRole, cancellationToken);
    }

    private static void EnsureAdmin(string actorRole)
    {
        if (!IsAdmin(actorRole))
        {
            throw new ForbiddenException("Only StudentAffairsAdmin can review budget proposals.");
        }
    }

    private static bool IsAdmin(string actorRole) =>
        string.Equals(actorRole, SystemRoleNames.StudentAffairsAdmin, StringComparison.Ordinal);

    private static BudgetProposalDto Map(BudgetProposal proposal) => new(
        proposal.Id,
        proposal.ClubId,
        proposal.ActivityId,
        proposal.ProposerId,
        proposal.EventName,
        proposal.RequestedAmount,
        proposal.ApprovedAmount,
        proposal.ProposedDate,
        proposal.ReviewedAt,
        proposal.ReviewedBy,
        proposal.Status.ToString(),
        proposal.Feedback,
        proposal.BudgetDetailsJson,
        proposal.ActualAmount,
        proposal.ReceiptUrl,
        proposal.SettlementDescription,
        proposal.SettledBy,
        proposal.SettledAt,
        proposal.CreatedAt,
        proposal.UpdatedAt);

    private static FinanceTransactionDto MapTransaction(FinanceTransaction transaction) => new(
        transaction.Id, transaction.ClubId, transaction.ReferenceId, transaction.Amount,
        transaction.Type.ToString(), transaction.Description, transaction.TransactionDate,
        transaction.ReceiptUrl, transaction.CreatedBy);
}
