using System;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Events.Commands.CreateEvent
{
    public class CreateEventCommand : IRequest<EventDto>
    {
        public Guid ClubId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ExpectedDate { get; set; }
        public string Location { get; set; } = string.Empty;
    }
}
