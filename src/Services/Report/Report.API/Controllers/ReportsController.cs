using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Report.Application.Features.Reports.Commands.CreateReport;
using Report.Application.Features.Reports.Commands.UpdateReport;
using Report.Application.Features.Reports.Commands.ReviewReport;
using Report.Application.Features.Reports.Commands.DeleteReport;
using Report.Application.Features.Reports.Queries.GetReportsByClub;
using Report.Application.Features.Reports.Queries.GetReportById;
using Shared.Kernel.Responses;
using Shared.Kernel.Exceptions;
using Report.Domain.Enums;
using Report.Application.DTOs;
using System.Collections.Generic;
using Shared.Kernel.Security;
using Report.Application.Features.Reports.Commands.SubmitReport;

namespace Report.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly Report.Application.Interfaces.IClubGrpcClient _club;
        private readonly Report.Application.Interfaces.ISmartReportSnapshotService _smartReportSnapshot;
        private readonly Report.Application.Interfaces.IReportValidationService _reportValidation;
        private readonly Report.Application.Interfaces.IReportDraftGenerationService _draftGeneration;

        public ReportsController(
            IMediator mediator,
            Report.Application.Interfaces.IClubGrpcClient club,
            Report.Application.Interfaces.ISmartReportSnapshotService smartReportSnapshot,
            Report.Application.Interfaces.IReportValidationService reportValidation,
            Report.Application.Interfaces.IReportDraftGenerationService draftGeneration)
        {
            _mediator = mediator;
            _club = club;
            _smartReportSnapshot = smartReportSnapshot;
            _reportValidation = reportValidation;
            _draftGeneration = draftGeneration;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null)
            {
                throw new UnauthorizedException("User is not authenticated.");
            }
            return Guid.Parse(userIdClaim.Value);
        }

        [Authorize]
        [HttpGet("smart-assistant/preview")]
        [ProducesResponseType(typeof(ApiResponse<ReportGenerationSnapshot>), StatusCodes.Status200OK)]
        public async Task<IActionResult> PreviewSmartAssistant(
            [FromQuery] Guid clubId,
            [FromQuery] Guid semesterId,
            CancellationToken cancellationToken)
        {
            if (clubId == Guid.Empty) throw new BadRequestException("clubId is required.");
            if (semesterId == Guid.Empty) throw new BadRequestException("semesterId is required.");
            if (!User.IsInRole(SystemRoleNames.StudentAffairsAdmin)
                && !await _club.CanSubmitReportsAsync(clubId, GetUserId(), cancellationToken))
                throw new ForbiddenException("Approved ClubLeader membership for the requested club is required.");

            var snapshot = await _smartReportSnapshot.GetPreviewAsync(clubId, semesterId, cancellationToken);
            return Ok(new ApiResponse<ReportGenerationSnapshot>(
                snapshot, "Smart report snapshot preview generated successfully."));
        }

        [Authorize]
        [HttpPost("smart-assistant/validate")]
        [ProducesResponseType(typeof(ApiResponse<ReportValidationResult>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ValidateSmartAssistant(
            [FromBody] ValidateReportRequest request,
            CancellationToken cancellationToken)
        {
            if (request.ClubId == Guid.Empty) throw new BadRequestException("clubId is required.");
            if (request.SemesterId == Guid.Empty) throw new BadRequestException("semesterId is required.");
            if (!User.IsInRole(SystemRoleNames.StudentAffairsAdmin)
                && !await _club.CanSubmitReportsAsync(request.ClubId, GetUserId(), cancellationToken))
                throw new ForbiddenException("Approved ClubLeader membership for the requested club is required.");

            var result = await _reportValidation.ValidateAsync(request, cancellationToken);
            return Ok(new ApiResponse<ReportValidationResult>(
                result, "Report validation completed successfully."));
        }

        [Authorize]
        [HttpPost("smart-assistant/generate")]
        [ProducesResponseType(typeof(ApiResponse<GeneratedReportDraft>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GenerateSmartAssistantDraft(
            [FromBody] GenerateReportDraftRequest request,
            CancellationToken cancellationToken)
        {
            if (request.ClubId == Guid.Empty) throw new BadRequestException("clubId is required.");
            if (request.SemesterId == Guid.Empty) throw new BadRequestException("semesterId is required.");
            if (!Enum.IsDefined(request.ReportType) || request.ReportType == 0)
                throw new BadRequestException("reportType is invalid.");
            if (!User.IsInRole(SystemRoleNames.StudentAffairsAdmin)
                && !await _club.CanSubmitReportsAsync(request.ClubId, GetUserId(), cancellationToken))
                throw new ForbiddenException("Approved ClubLeader membership for the requested club is required.");

            var draft = await _draftGeneration.GenerateAsync(request, cancellationToken);
            return Ok(new ApiResponse<GeneratedReportDraft>(
                draft, "Rule-based report draft generated successfully."));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
        {
            var command = new CreateReportCommand
            {
                ClubId = request.ClubId,
                SemesterId = request.SemesterId,
                Title = request.Title,
                Content = request.Content,
                Type = request.Type,
                CreatedBy = GetUserId(),
                Attachments = request.Attachments
            };

            var result = await _mediator.Send(command);
            var response = new ApiResponse<ReportDto>(result, "Report created successfully.");
            return Ok(response);
        }

        [Authorize]
        [HttpPost("{id:guid}/submit")]
        public async Task<IActionResult> SubmitReport(Guid id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new SubmitReportCommand
            {
                ReportId = id,
                UserId = GetUserId()
            }, cancellationToken);
            return Ok(new ApiResponse<ReportDto>(result, "Report submitted successfully."));
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReport(Guid id, [FromBody] UpdateReportRequest request)
        {
            var command = new UpdateReportCommand
            {
                Id = id,
                Title = request.Title,
                Content = request.Content,
                Type = request.Type,
                UserId = GetUserId()
            };

            var result = await _mediator.Send(command);
            var response = new ApiResponse<ReportDto>(result, "Report updated successfully.");
            return Ok(response);
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPut("{id}/review")]
        public async Task<IActionResult> ReviewReport(Guid id, [FromBody] ReviewReportRequest request)
        {
            var command = new ReviewReportCommand
            {
                ReportId = id,
                UserId = GetUserId(),
                ActorRole = User.FindFirst("role")?.Value ?? string.Empty,
                IsApproved = request.IsApproved ?? false,
                Action = request.Action,
                ReviewNote = request.ReviewNote
            };

            var result = await _mediator.Send(command);
            var action = request.Action ?? (request.IsApproved == true ? "approved" : "rejected");
            var response = new ApiResponse<ReportDto>(result, $"Report {action} successfully.");
            return Ok(response);
        }

        [Authorize]
        [HttpGet("{id:guid}/history")]
        public async Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken)
        {
            var report = await _mediator.Send(new GetReportByIdQuery { Id = id }, cancellationToken);
            if (!User.IsInRole(SystemRoleNames.StudentAffairsAdmin))
            {
                var allowed = await _club.CanSubmitReportsAsync(report.ClubId, GetUserId(), cancellationToken);
                if (!allowed) throw new ForbiddenException("You cannot view another club's report history.");
            }
            var history = await HttpContext.RequestServices.GetRequiredService<Report.Application.Interfaces.IReportUnitOfWork>()
                .GetHistoryAsync(id, cancellationToken);
            var result = history.Select(x => new ReportRevisionHistoryDto(
                x.Id, x.ReportId, x.RevisionNumber, x.PreviousStatus.ToString(), x.NewStatus.ToString(),
                x.Feedback, x.ChangedBy, x.ChangedAt));
            return Ok(new ApiResponse<object>(result, "Report history fetched successfully."));
        }

        [Authorize]
        [HttpGet("club/{clubId}")]
        public async Task<IActionResult> GetReportsByClub(Guid clubId, [FromQuery] ReportStatus? status, [FromQuery] ReportType? type)
        {
            if (!User.IsInRole(SystemRoleNames.StudentAffairsAdmin)
                && !await _club.CanSubmitReportsAsync(clubId, GetUserId(), HttpContext.RequestAborted))
                throw new ForbiddenException("You cannot view another club's reports.");

            var query = new GetReportsByClubQuery
            {
                ClubId = clubId,
                Status = status,
                Type = type
            };

            var result = await _mediator.Send(query);
            var response = new ApiResponse<IEnumerable<ReportDto>>(result, "Fetched reports successfully.");
            return Ok(response);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReportById(Guid id)
        {
            var query = new GetReportByIdQuery { Id = id };
            var result = await _mediator.Send(query);
            if (!User.IsInRole(SystemRoleNames.StudentAffairsAdmin)
                && !await _club.CanSubmitReportsAsync(result.ClubId, GetUserId(), HttpContext.RequestAborted))
                throw new ForbiddenException("You cannot view another club's report.");
            var response = new ApiResponse<ReportDto>(result, "Fetched report successfully.");
            return Ok(response);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReport(Guid id)
        {
            var command = new DeleteReportCommand
            {
                Id = id,
                UserId = GetUserId()
            };

            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Report deleted successfully."));
        }
    }

    public class CreateReportRequest
    {
        [Required(ErrorMessage = "Club ID is required")]
        public Guid ClubId { get; set; }
        [Required]
        public Guid SemesterId { get; set; }

        [Required(ErrorMessage = "Report title is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Report title must be between 3 and 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Report content is required")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Report type is required")]
        [Range(1, 3, ErrorMessage = "Invalid report type (1: Financial, 2: Activity, 3: General)")]
        public ReportType Type { get; set; }

        public List<AttachmentInput>? Attachments { get; set; }
    }

    public class UpdateReportRequest
    {
        [Required(ErrorMessage = "Report title is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Report title must be between 3 and 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Report content is required")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Report type is required")]
        [Range(1, 3, ErrorMessage = "Invalid report type (1: Financial, 2: Activity, 3: General)")]
        public ReportType Type { get; set; }
    }

    public class ReviewReportRequest
    {
        public bool? IsApproved { get; set; }
        public string? Action { get; set; }

        [StringLength(500, ErrorMessage = "Review note cannot exceed 500 characters")]
        public string? ReviewNote { get; set; }
    }
}
