using System;
using Club.Application.DTOs;
using MediatR;

namespace Club.Application.Features.Events.Queries.GetEventById
{
    public class GetEventByIdQuery : IRequest<EventDto>
    {
        public Guid Id { get; set; }
    }
}
