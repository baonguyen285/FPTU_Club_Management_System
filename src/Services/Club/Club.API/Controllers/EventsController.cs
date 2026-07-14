using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Club.Application.Features.Events.Commands.CreateEvent;
using Club.Application.Features.Events.Commands.UpdateEvent;
using Club.Application.Features.Events.Commands.SoftDeleteEvent;
using Club.Application.Features.Events.Commands.HardDeleteEvent;
using Club.Application.Features.Events.Queries.GetEventsByClub;
using Club.Application.Features.Events.Queries.GetEventById;
using Shared.Kernel.Responses;

namespace Club.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public EventsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("club/{clubId}")]
        public async Task<IActionResult> GetEventsByClub(Guid clubId)
        {
            var query = new GetEventsByClubQuery(clubId);
            var result = await _mediator.Send(query);
            return Ok(new ApiResponse<object>(result, "Retrieved events successfully."));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEventById(Guid id)
        {
            var query = new GetEventByIdQuery { Id = id };
            var result = await _mediator.Send(query);
            return Ok(new ApiResponse<object>(result, "Retrieved event successfully."));
        }

        [Authorize(Roles = "Admin,ClubManager")]
        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Event created successfully."));
        }

        [Authorize(Roles = "Admin,ClubManager")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventCommand command)
        {
            command.Id = id;
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Event updated successfully."));
        }

        /// <summary>
        /// Xóa mềm sự kiện (hủy sự kiện - giữ lại lịch sử)
        /// </summary>
        [Authorize(Roles = "Admin,ClubManager")]
        [HttpDelete("{id}/cancel")]
        public async Task<IActionResult> SoftDeleteEvent(Guid id)
        {
            var command = new SoftDeleteEventCommand(id);
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Event cancelled successfully (soft delete)."));
        }

        /// <summary>
        /// Xóa vĩnh viễn sự kiện khỏi Database
        /// </summary>
        [Authorize(Roles = "Admin,ClubManager")]
        [HttpDelete("{id}/permanent")]
        public async Task<IActionResult> HardDeleteEvent(Guid id)
        {
            var command = new HardDeleteEventCommand(id);
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Event permanently deleted."));
        }
    }
}
