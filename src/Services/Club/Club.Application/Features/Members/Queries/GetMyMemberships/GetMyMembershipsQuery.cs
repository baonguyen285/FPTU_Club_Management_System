using System;
using System.Collections.Generic;
using Club.Application.DTOs;
using MediatR;

namespace Club.Application.Features.Members.Queries.GetMyMemberships
{
    public class GetMyMembershipsQuery : IRequest<IEnumerable<ClubMemberDto>>
    {
        public Guid UserId { get; set; }
    }
}
