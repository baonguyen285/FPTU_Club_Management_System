namespace Finance.Application.DTOs;

public sealed record BudgetProposalDto(
    Guid Id,
    Guid ClubId,
    Guid? ActivityId,
    Guid ProposerId,
    string EventName,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    DateTime ProposedDate,
    DateTime? ReviewedAt,
    Guid? ReviewedBy,
    string Status,
    string? Feedback,
    string? BudgetDetailsJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateBudgetProposalCommand(
    Guid ClubId,
    Guid? ActivityId,
    string EventName,
    decimal RequestedAmount,
    string? BudgetDetailsJson);

public sealed record UpdateBudgetProposalCommand(
    Guid? ActivityId,
    string EventName,
    decimal RequestedAmount,
    string? BudgetDetailsJson);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
