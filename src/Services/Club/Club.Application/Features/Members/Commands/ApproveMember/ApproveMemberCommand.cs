using System;
using Club.Application.DTOs;
using MediatR;

namespace Club.Application.Features.Members.Commands.ApproveMember
{
    public class ApproveMemberCommand : IRequest<ClubMemberDto>
    {
        public Guid ClubId { get; set; }
        public Guid UserId { get; set; }
    }
}
