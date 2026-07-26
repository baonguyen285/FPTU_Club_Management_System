using Finance.Domain.Enums;
using Shared.Kernel.Domain;

namespace Finance.Domain.Entities;

public class BudgetProposal : BaseEntity
{
    public Guid ClubId { get; private set; }
    public Guid? ActivityId { get; private set; }
    public Guid ProposerId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public decimal RequestedAmount { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public DateTime ProposedDate { get; private set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public BudgetProposalStatus Status { get; private set; } = BudgetProposalStatus.Draft;
    public string? Feedback { get; private set; }
    public string? BudgetDetailsJson { get; private set; }
    public decimal? ActualAmount { get; private set; }
    public string? ReceiptUrl { get; private set; }
    public string? SettlementDescription { get; private set; }
    public Guid? SettledBy { get; private set; }
    public DateTime? SettledAt { get; private set; }

    private BudgetProposal()
    {
    }

    public BudgetProposal(
        Guid clubId,
        Guid? activityId,
        Guid proposerId,
        string eventName,
        decimal requestedAmount,
        string? budgetDetailsJson)
    {
        Validate(clubId, proposerId, eventName, requestedAmount);

        Id = Guid.NewGuid();
        ClubId = clubId;
        ActivityId = activityId;
        ProposerId = proposerId;
        EventName = eventName.Trim();
        RequestedAmount = requestedAmount;
        BudgetDetailsJson = budgetDetailsJson?.Trim();
        ProposedDate = DateTime.UtcNow;
        Status = BudgetProposalStatus.Draft;
    }

    public void Update(Guid? activityId, string eventName, decimal requestedAmount, string? budgetDetailsJson)
    {
        EnsureStatus(BudgetProposalStatus.Draft, "Only draft proposals can be updated.");
        Validate(ClubId, ProposerId, eventName, requestedAmount);

        ActivityId = activityId;
        EventName = eventName.Trim();
        RequestedAmount = requestedAmount;
        BudgetDetailsJson = budgetDetailsJson?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Submit()
    {
        EnsureStatus(BudgetProposalStatus.Draft, "Only draft proposals can be submitted.");
        Status = BudgetProposalStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewerId)
    {
        EnsurePending();
        ApplyReview(reviewerId, BudgetProposalStatus.Approved, RequestedAmount, null);
    }

    public void PartiallyApprove(Guid reviewerId, decimal approvedAmount, string feedback)
    {
        EnsurePending();
        if (approvedAmount <= 0 || approvedAmount >= RequestedAmount)
        {
            throw new Shared.Kernel.Exceptions.InvalidDomainException(
                "Partial approval amount must be greater than zero and lower than the requested amount.");
        }

        if (string.IsNullOrWhiteSpace(feedback))
        {
            throw new Shared.Kernel.Exceptions.InvalidDomainException("Feedback is required for partial approval.");
        }

        ApplyReview(reviewerId, BudgetProposalStatus.PartiallyApproved, approvedAmount, feedback.Trim());
    }

    public void Reject(Guid reviewerId, string feedback)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(feedback))
        {
            throw new Shared.Kernel.Exceptions.InvalidDomainException("A rejection reason is required.");
        }

        ApplyReview(reviewerId, BudgetProposalStatus.Rejected, null, feedback.Trim());
    }

    public void Settle(Guid actorId, decimal actualAmount, string receiptUrl, string? description)
    {
        if (Status is not BudgetProposalStatus.Approved and not BudgetProposalStatus.PartiallyApproved)
            throw new Shared.Kernel.Exceptions.ConflictException("Only approved proposals can be settled.");
        if (actualAmount <= 0)
            throw new Shared.Kernel.Exceptions.InvalidDomainException("Actual amount must be greater than zero.");
        if (!ApprovedAmount.HasValue || actualAmount > ApprovedAmount.Value)
            throw new Shared.Kernel.Exceptions.InvalidDomainException("Actual amount cannot exceed approved amount.");
        if (string.IsNullOrWhiteSpace(receiptUrl))
            throw new Shared.Kernel.Exceptions.InvalidDomainException("Receipt URL is required.");

        ActualAmount = actualAmount;
        ReceiptUrl = receiptUrl.Trim();
        SettlementDescription = description?.Trim();
        SettledBy = actorId;
        SettledAt = DateTime.UtcNow;
        Status = BudgetProposalStatus.Settled;
        UpdatedAt = SettledAt;
    }

    private static void Validate(Guid clubId, Guid proposerId, string eventName, decimal requestedAmount)
    {
        if (clubId == Guid.Empty || proposerId == Guid.Empty)
        {
            throw new Shared.Kernel.Exceptions.InvalidDomainException("Club and proposer are required.");
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new Shared.Kernel.Exceptions.InvalidDomainException("Event name is required.");
        }

        if (requestedAmount <= 0)
        {
            throw new Shared.Kernel.Exceptions.InvalidDomainException("Requested amount must be greater than zero.");
        }
    }

    private void EnsurePending() =>
        EnsureStatus(BudgetProposalStatus.Pending, "Only pending proposals can be reviewed.");

    private void EnsureStatus(BudgetProposalStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new Shared.Kernel.Exceptions.ConflictException(message);
        }
    }

    private void ApplyReview(
        Guid reviewerId,
        BudgetProposalStatus status,
        decimal? approvedAmount,
        string? feedback)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new Shared.Kernel.Exceptions.InvalidDomainException("Reviewer is required.");
        }

        Status = status;
        ApprovedAmount = approvedAmount;
        Feedback = feedback;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
