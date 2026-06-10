using System.Collections.Generic;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Clubs.Queries.GetClubs
{
    public class GetClubsQuery : IRequest<IEnumerable<ClubDto>>
    {
    }
}
