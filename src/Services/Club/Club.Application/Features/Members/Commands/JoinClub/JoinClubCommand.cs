using System;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Members.Commands.JoinClub
{
    public class JoinClubCommand : IRequest<ClubMemberDto>
    {
        public Guid ClubId { get; set; }
        public Guid UserId { get; set; }
    }
}
