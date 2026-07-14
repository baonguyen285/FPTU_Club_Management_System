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

namespace Report.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ReportsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                throw new UnauthorizedException("User is not authenticated.");
            }
            return Guid.Parse(userIdClaim.Value);
        }

        [Authorize(Roles = "ClubManager")]
        [HttpPost]
        public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
        {
            var command = new CreateReportCommand
            {
                ClubId = request.ClubId,
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

        [Authorize(Roles = "ClubManager")]
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

        [Authorize(Roles = "Admin,Advisor")]
        [HttpPut("{id}/review")]
        public async Task<IActionResult> ReviewReport(Guid id, [FromBody] ReviewReportRequest request)
        {
            var command = new ReviewReportCommand
            {
                ReportId = id,
                UserId = GetUserId(),
                IsApproved = request.IsApproved,
                ReviewNote = request.ReviewNote
            };

            var result = await _mediator.Send(command);
            var action = request.IsApproved ? "approved" : "rejected";
            var response = new ApiResponse<ReportDto>(result, $"Report {action} successfully.");
            return Ok(response);
        }

        [Authorize(Roles = "Admin,Advisor,ClubManager")]
        [HttpGet("club/{clubId}")]
        public async Task<IActionResult> GetReportsByClub(Guid clubId, [FromQuery] ReportStatus? status, [FromQuery] ReportType? type)
        {
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

        [Authorize(Roles = "Admin,Advisor,ClubManager")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReportById(Guid id)
        {
            var query = new GetReportByIdQuery { Id = id };
            var result = await _mediator.Send(query);
            var response = new ApiResponse<ReportDto>(result, "Fetched report successfully.");
            return Ok(response);
        }

        [Authorize(Roles = "Admin,Advisor,ClubManager")]
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
        [Required(ErrorMessage = "Review approval status is required")]
        public bool IsApproved { get; set; }

        [StringLength(500, ErrorMessage = "Review note cannot exceed 500 characters")]
        public string? ReviewNote { get; set; }
    }
}
