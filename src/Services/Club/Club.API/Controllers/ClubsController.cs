using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Club.Application.Features.Clubs.Commands.CreateClub;
using Club.Application.Features.Clubs.Commands.UpdateClub;
using Club.Application.Features.Clubs.Commands.DeleteClub;
using Club.Application.Features.Clubs.Commands.ReviewClub;
using Club.Application.Features.Clubs.Queries.GetClubs;
using Club.Application.Features.Clubs.Queries.GetClubById;
using Club.Application.Features.Members.Commands.JoinClub;
using Club.Application.Features.Members.Commands.ApproveMember;
using Club.Application.Features.Members.Commands.RejectMember;
using Club.Application.Features.Members.Commands.UpdateMemberRole;
using Club.Application.Features.Members.Commands.RemoveMember;
using Club.Application.Features.Members.Queries.GetClubMembers;
using Club.Application.Features.Members.Queries.GetMyMemberships;
using Shared.Kernel.Responses;
using Shared.Kernel.Security;
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

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPost]
        public async Task<IActionResult> CreateClub([FromBody] CreateClubCommand command)
        {
            command.ActorId = GetActorId();
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Club created successfully."));
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClub(Guid id, [FromBody] UpdateClubCommand command)
        {
            command.Id = id;
            command.ActorId = GetActorId();
            command.ActorRole = GetActorRole();
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Club updated successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClub(Guid id)
        {
            var command = new DeleteClubCommand(id);
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Club deactivated successfully (soft delete)."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPut("{id}/review")]
        public async Task<IActionResult> ReviewClub(Guid id, [FromBody] ReviewClubCommand command)
        {
            command.Id = id;
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Club reviewed successfully."));
        }

        // ==================== MEMBER APIs ====================

        [HttpGet("{id}/members")]
        public async Task<IActionResult> GetClubMembers(Guid id)
        {
            var query = new GetClubMembersQuery(id);
            var result = await _mediator.Send(query);
            return Ok(new ApiResponse<object>(result, "Retrieved club members successfully."));
        }

        [Authorize]
        [HttpGet("my-memberships")]
        public async Task<IActionResult> GetMyMemberships()
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null)
            {
                throw new UnauthorizedException("User is not authenticated.");
            }

            var query = new GetMyMembershipsQuery { UserId = Guid.Parse(userIdClaim.Value) };
            var result = await _mediator.Send(query);
            return Ok(new ApiResponse<object>(result, "Retrieved memberships successfully."));
        }

        [Authorize(Roles = "Student")]
        [HttpPost("{id}/members")]
        public async Task<IActionResult> JoinClub(Guid id)
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null)
            {
                throw new UnauthorizedException("User is not authenticated.");
            }

            var userId = Guid.Parse(userIdClaim.Value);
            var command = new JoinClubCommand { ClubId = id, UserId = userId };
            
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Joined club successfully."));
        }

        [Authorize]
        [HttpPut("{id}/members/{userId}/approve")]
        public async Task<IActionResult> ApproveMember(Guid id, Guid userId)
        {
            var command = new ApproveMemberCommand
            {
                ClubId = id, UserId = userId, ActorId = GetActorId(), ActorRole = GetActorRole()
            };
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Member approved successfully."));
        }

        [Authorize]
        [HttpPut("{id}/members/{userId}/reject")]
        public async Task<IActionResult> RejectMember(Guid id, Guid userId)
        {
            var command = new RejectMemberCommand
            {
                ClubId = id, UserId = userId, ActorId = GetActorId(), ActorRole = GetActorRole()
            };
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Member rejected successfully."));
        }

        [Authorize]
        [HttpPut("{id}/members/{userId}/role")]
        public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, [FromBody] UpdateMemberRoleCommand command)
        {
            command.ClubId = id;
            command.UserId = userId;
            command.ActorId = GetActorId();
            command.ActorRole = GetActorRole();
            var result = await _mediator.Send(command);
            return Ok(new ApiResponse<object>(result, "Member role updated successfully."));
        }

        [Authorize]
        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            var command = new RemoveMemberCommand(id, userId, GetActorId(), GetActorRole());
            await _mediator.Send(command);
            return Ok(new ApiResponse<object>(null, "Member removed from club successfully."));
        }

        private Guid GetActorId()
        {
            var value = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(value, out var actorId)
                ? actorId
                : throw new UnauthorizedException("User is not authenticated.");
        }

        private string GetActorRole() =>
            User.FindFirst("role")?.Value ?? throw new UnauthorizedException("Role claim is missing.");
    }
}
