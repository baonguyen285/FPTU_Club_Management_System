using System;
using MediatR;

namespace Club.Application.Features.Members.Commands.RejectMember
{
    public class RejectMemberCommand : IRequest
    {
        public Guid ClubId { get; set; }
        public Guid UserId { get; set; }
        public Guid ActorId { get; set; }
        public string ActorRole { get; set; } = string.Empty;
    }
}
