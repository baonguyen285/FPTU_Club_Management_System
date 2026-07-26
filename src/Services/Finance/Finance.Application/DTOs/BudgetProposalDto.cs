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
    decimal? ActualAmount,
    string? ReceiptUrl,
    string? SettlementDescription,
    Guid? SettledBy,
    DateTime? SettledAt,
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

public sealed record FinanceTransactionDto(
    Guid Id, Guid ClubId, Guid? ReferenceId, decimal Amount, string Type,
    string Description, DateTime TransactionDate, string? ReceiptUrl, Guid CreatedBy);

public sealed record ClubFinanceBalanceDto(
    Guid ClubId, decimal AllocatedAmount, decimal SpentAmount, decimal AvailableAmount, DateTime? UpdatedAt);

public sealed record SettleBudgetProposalCommand(decimal ActualAmount, string ReceiptUrl, string? Description);

public sealed record CreateFinanceTransactionCommand(
    Guid ClubId, Guid? ReferenceId, decimal Amount, Finance.Domain.Enums.FinanceTransactionType Type,
    string Description, string? ReceiptUrl);
