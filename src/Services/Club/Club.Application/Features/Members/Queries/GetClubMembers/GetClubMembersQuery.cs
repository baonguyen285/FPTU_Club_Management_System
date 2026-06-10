using System;
using System.Collections.Generic;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Members.Queries.GetClubMembers
{
    public class GetClubMembersQuery : IRequest<IEnumerable<ClubMemberDto>>
    {
        public Guid ClubId { get; set; }

        public GetClubMembersQuery(Guid clubId)
        {
            ClubId = clubId;
        }
    }
}
