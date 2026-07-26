using System;
using System.ComponentModel.DataAnnotations;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Events.Commands.UpdateEvent
{
    public class UpdateEventCommand : IRequest<EventDto>
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid Id { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid ActorId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string ActorRole { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event title is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Event title must be between 3 and 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Expected date is required")]
        public DateTime ExpectedDate { get; set; }

        [Required(ErrorMessage = "Event location is required")]
        [StringLength(250, ErrorMessage = "Location cannot exceed 250 characters")]
        public string Location { get; set; } = string.Empty;
    }
}
