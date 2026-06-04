using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Report.Application.Features.Reports.Commands.SubmitReport;
using Shared.Kernel.Responses;
using Shared.Kernel.Exceptions;

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

        [Authorize]
        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitReport(Guid id, [FromBody] SubmitReportRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                throw new UnauthorizedException("User is not authenticated.");
            }

            var userId = Guid.Parse(userIdClaim.Value);

            var command = new SubmitReportCommand(
                reportId: id,
                clubId: request.ClubId,
                title: request.Title,
                content: request.Content,
                userId: userId
            );

            var result = await _mediator.Send(command);

            var response = new ApiResponse<bool>(result, "Report submitted successfully.");
            return Ok(response);
        }
    }

    public class SubmitReportRequest
    {
        public Guid ClubId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }
}
