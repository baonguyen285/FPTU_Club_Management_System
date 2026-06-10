using System;
using System.Collections.Generic;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Events.Queries.GetEventsByClub
{
    public class GetEventsByClubQuery : IRequest<IEnumerable<EventDto>>
    {
        public Guid ClubId { get; set; }
        
        public GetEventsByClubQuery(Guid clubId)
        {
            ClubId = clubId;
        }
    }
}
