using Finance.Application.DTOs;
using Finance.Application.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Shared.Kernel.Exceptions;

namespace Finance.Application.Services;

public sealed class BudgetProposalService : IBudgetProposalService
{
    private readonly IBudgetProposalRepository _repository;
    private readonly IClubAccessService _clubAccess;

    public BudgetProposalService(IBudgetProposalRepository repository, IClubAccessService clubAccess)
    {
        _repository = repository;
        _clubAccess = clubAccess;
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
        await _repository.SaveChangesAsync(cancellationToken);
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
        await _repository.SaveChangesAsync(cancellationToken);
        return Map(proposal);
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
            throw new ForbiddenException("Only Admin can review budget proposals.");
        }
    }

    private static bool IsAdmin(string actorRole) =>
        string.Equals(actorRole, "Admin", StringComparison.OrdinalIgnoreCase);

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
        proposal.CreatedAt,
        proposal.UpdatedAt);
}
