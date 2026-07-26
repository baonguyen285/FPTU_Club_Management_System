using System;
using MediatR;
using Club.Application.DTOs;
using Club.Domain.Enums;

namespace Club.Application.Features.Members.Commands.UpdateMemberRole
{
    public class UpdateMemberRoleCommand : IRequest<ClubMemberDto>
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid ClubId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid UserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid ActorId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string ActorRole { get; set; } = string.Empty;
        public ClubRole NewRole { get; set; }
        public MembershipStatus NewStatus { get; set; }
    }
}
