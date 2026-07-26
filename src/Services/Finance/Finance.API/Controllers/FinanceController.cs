using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Finance.Application.DTOs;
using Finance.Application.Interfaces;
using Finance.Domain.Enums;
using Finance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;
using Shared.Kernel.Security;

namespace Finance.API.Controllers;

[ApiController]
[Route("api/v1/finance")]
public class FinanceController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;
    private readonly IBudgetProposalService _proposalService;

    public FinanceController(FinanceDbContext dbContext, IBudgetProposalService proposalService)
    {
        _dbContext = dbContext;
        _proposalService = proposalService;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<FinanceServiceStatusDto>), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        var status = new FinanceServiceStatusDto(
            Service: "finance-service",
            Version: "v1",
            DatabaseConfigured: _dbContext.Database.ProviderName is not null);

        return Ok(new ApiResponse<FinanceServiceStatusDto>(status, "Finance service is ready."));
    }

    [Authorize]
    [HttpPost("proposals")]
    [ProducesResponseType(typeof(ApiResponse<BudgetProposalDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProposal(
        [FromBody] CreateBudgetProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await _proposalService.CreateAsync(
            new CreateBudgetProposalCommand(
                request.ClubId,
                request.ActivityId,
                request.EventName,
                request.RequestedAmount,
                request.BudgetDetailsJson),
            GetActorId(),
            GetActorRole(),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetProposalById),
            new { id = proposal.Id },
            new ApiResponse<BudgetProposalDto>(proposal, "Budget proposal created as draft."));
    }

    [Authorize]
    [HttpGet("proposals")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BudgetProposalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProposals(
        [FromQuery] Guid? clubId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        BudgetProposalStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BudgetProposalStatus>(status, true, out var value))
            {
                throw new BadRequestException("Invalid budget proposal status.");
            }

            parsedStatus = value;
        }

        var result = await _proposalService.GetAsync(
            GetActorId(),
            GetActorRole(),
            clubId,
            parsedStatus,
            page,
            pageSize,
            cancellationToken);

        return Ok(new ApiResponse<IReadOnlyList<BudgetProposalDto>>(
            result.Items,
            "Budget proposals retrieved successfully.",
            meta: new
            {
                result.Page,
                result.PageSize,
                result.TotalItems,
                result.TotalPages
            }));
    }

    [Authorize]
    [HttpGet("proposals/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BudgetProposalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProposalById(Guid id, CancellationToken cancellationToken)
    {
        var proposal = await _proposalService.GetByIdAsync(
            id,
            GetActorId(),
            GetActorRole(),
            cancellationToken);
        return Ok(new ApiResponse<BudgetProposalDto>(proposal, "Budget proposal retrieved successfully."));
    }

    [Authorize]
    [HttpPut("proposals/{id:guid}")]
    public async Task<IActionResult> UpdateProposal(
        Guid id,
        [FromBody] UpdateBudgetProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await _proposalService.UpdateAsync(
            id,
            new UpdateBudgetProposalCommand(
                request.ActivityId,
                request.EventName,
                request.RequestedAmount,
                request.BudgetDetailsJson),
            GetActorId(),
            GetActorRole(),
            cancellationToken);
        return Ok(new ApiResponse<BudgetProposalDto>(proposal, "Budget proposal updated successfully."));
    }

    [Authorize]
    [HttpPost("proposals/{id:guid}/submit")]
    public async Task<IActionResult> SubmitProposal(Guid id, CancellationToken cancellationToken)
    {
        var proposal = await _proposalService.SubmitAsync(
            id,
            GetActorId(),
            GetActorRole(),
            cancellationToken);
        return Ok(new ApiResponse<BudgetProposalDto>(proposal, "Budget proposal submitted successfully."));
    }

    [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
    [HttpPost("proposals/{id:guid}/approve")]
    public async Task<IActionResult> ApproveProposal(Guid id, CancellationToken cancellationToken)
    {
        var proposal = await _proposalService.ApproveAsync(
            id,
            GetActorId(),
            GetActorRole(),
            cancellationToken);
        return Ok(new ApiResponse<BudgetProposalDto>(proposal, "Budget proposal approved successfully."));
    }

    [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
    [HttpPost("proposals/{id:guid}/partial-approve")]
    public async Task<IActionResult> PartiallyApproveProposal(
        Guid id,
        [FromBody] PartialApproveBudgetProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await _proposalService.PartiallyApproveAsync(
            id,
            request.ApprovedAmount,
            request.Feedback,
            GetActorId(),
            GetActorRole(),
            cancellationToken);
        return Ok(new ApiResponse<BudgetProposalDto>(proposal, "Budget proposal partially approved."));
    }

    [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
    [HttpPost("proposals/{id:guid}/reject")]
    public async Task<IActionResult> RejectProposal(
        Guid id,
        [FromBody] RejectBudgetProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await _proposalService.RejectAsync(
            id,
            request.Feedback,
            GetActorId(),
            GetActorRole(),
            cancellationToken);
        return Ok(new ApiResponse<BudgetProposalDto>(proposal, "Budget proposal rejected."));
    }

    [Authorize]
    [HttpPost("proposals/{id:guid}/settle")]
    public async Task<IActionResult> SettleProposal(
        Guid id, [FromBody] SettleBudgetProposalRequest request, CancellationToken cancellationToken)
    {
        var proposal = await _proposalService.SettleAsync(
            id, new SettleBudgetProposalCommand(request.ActualAmount, request.ReceiptUrl, request.Description),
            GetActorId(), GetActorRole(), cancellationToken);
        return Ok(new ApiResponse<BudgetProposalDto>(proposal, "Budget proposal settled successfully."));
    }

    [Authorize]
    [HttpGet("clubs/{clubId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid clubId, CancellationToken cancellationToken)
    {
        var balance = await _proposalService.GetBalanceAsync(
            clubId, GetActorId(), GetActorRole(), cancellationToken);
        return Ok(new ApiResponse<ClubFinanceBalanceDto>(balance, "Club balance retrieved successfully."));
    }

    [Authorize]
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] Guid clubId, CancellationToken cancellationToken)
    {
        if (clubId == Guid.Empty) throw new BadRequestException("clubId is required.");
        var transactions = await _proposalService.GetTransactionsAsync(
            clubId, GetActorId(), GetActorRole(), cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<FinanceTransactionDto>>(transactions, "Finance transactions retrieved successfully."));
    }

    [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction(
        [FromBody] CreateFinanceTransactionRequest request, CancellationToken cancellationToken)
    {
        var transaction = await _proposalService.CreateTransactionAsync(
            new CreateFinanceTransactionCommand(request.ClubId, request.ReferenceId, request.Amount,
                request.Type, request.Description, request.ReceiptUrl),
            GetActorId(), GetActorRole(), cancellationToken);
        return StatusCode(201, new ApiResponse<FinanceTransactionDto>(transaction, "Finance transaction created."));
    }

    private Guid GetActorId()
    {
        var value = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var actorId)
            ? actorId
            : throw new UnauthorizedException("Invalid identity token.");
    }

    private string GetActorRole() =>
        User.FindFirstValue("role")
        ?? throw new UnauthorizedException("Role claim is missing.");
}

public sealed class CreateBudgetProposalRequest
{
    public Guid ClubId { get; set; }
    public Guid? ActivityId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string EventName { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal RequestedAmount { get; set; }

    public string? BudgetDetailsJson { get; set; }
}

public sealed class UpdateBudgetProposalRequest
{
    public Guid? ActivityId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string EventName { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal RequestedAmount { get; set; }

    public string? BudgetDetailsJson { get; set; }
}

public sealed class PartialApproveBudgetProposalRequest
{
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal ApprovedAmount { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string Feedback { get; set; } = string.Empty;
}

public sealed class RejectBudgetProposalRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string Feedback { get; set; } = string.Empty;
}

public sealed class SettleBudgetProposalRequest
{
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal ActualAmount { get; set; }
    [Required, Url, StringLength(1000)]
    public string ReceiptUrl { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Description { get; set; }
}

public sealed class CreateFinanceTransactionRequest
{
    [Required]
    public Guid ClubId { get; set; }
    public Guid? ReferenceId { get; set; }
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Amount { get; set; }
    public FinanceTransactionType Type { get; set; }
    [Required, StringLength(500, MinimumLength = 3)]
    public string Description { get; set; } = string.Empty;
    [Url, StringLength(1000)]
    public string? ReceiptUrl { get; set; }
}
