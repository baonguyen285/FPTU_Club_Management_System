using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Club.Application.Features.Clubs.Commands.CreateClub;
using Club.Application.Features.Clubs.Commands.UpdateClub;
using Club.Application.Features.Clubs.Commands.DeleteClub;
using Club.Application.Features.Clubs.Queries.GetClubs;
using Club.Application.Features.Clubs.Queries.GetClubById;
using Club.Application.Features.Members.Commands.JoinClub;
using Club.Application.Features.Members.Commands.UpdateMemberRole;
using Club.Application.Features.Members.Commands.RemoveMember;
using Club.Application.Features.Members.Queries.GetClubMembers;
using Shared.Kernel.Responses;
using System.Security.Claims;
using Shared.Kernel.Exceptions;

namespace Club.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ClubsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ClubsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ==================== CLUB APIs ====================

        [HttpGet]
        public async Task<IActionResult> GetClubs()
        {
            var query = new GetClubsQuery();
            var result = await _mediator.Send(query);
            return Ok(new ApiResponse<object>(result, "Retrieved clubs successfully."));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetClubById(Guid id)
        {
            var query = new GetClubByIdQuery(id);
            var result = await _mediator.Send(query);
            return Ok(new ApiResponse<object>(result, "Retrieved club successfully."));
        }

        [Authorize(Roles = "Admin,Advisor")]
        [HttpPost]
        public async Task<IActionResult> CreateClub([FromBody] CreateClubCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Club created successfully."));
        }

        [Authorize(Roles = "Admin,ClubManager")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClub(Guid id, [FromBody] UpdateClubCommand command)
        {
            command.Id = id;
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Club updated successfully."));
        }

        [Authorize(Roles = "Admin,Advisor")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClub(Guid id)
        {
            var command = new DeleteClubCommand(id);
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Club deactivated successfully (soft delete)."));
        }

        // ==================== MEMBER APIs ====================

        [HttpGet("{id}/members")]
        public async Task<IActionResult> GetClubMembers(Guid id)
        {
            var query = new GetClubMembersQuery(id);
            var result = await _mediator.Send(query);
            return Ok(new ApiResponse<object>(result, "Retrieved club members successfully."));
        }

        [Authorize(Roles = "Student")]
        [HttpPost("{id}/members")]
        public async Task<IActionResult> JoinClub(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                throw new UnauthorizedException("User is not authenticated.");
            }

            var userId = Guid.Parse(userIdClaim.Value);
            var command = new JoinClubCommand { ClubId = id, UserId = userId };
            
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Joined club successfully."));
        }

        [Authorize(Roles = "Admin,ClubManager")]
        [HttpPut("{id}/members/{userId}/role")]
        public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, [FromBody] UpdateMemberRoleCommand command)
        {
            command.ClubId = id;
            command.UserId = userId;
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Member role updated successfully."));
        }

        [Authorize(Roles = "Admin,ClubManager")]
        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            var command = new RemoveMemberCommand(id, userId);
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Member removed from club successfully."));
        }
    }
}
