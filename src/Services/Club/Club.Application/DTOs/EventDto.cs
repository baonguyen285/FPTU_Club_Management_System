using System;
using Club.Domain.Enums;

namespace Club.Application.DTOs
{
    public class EventDto
    {
        public Guid Id { get; set; }
        public Guid ClubId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ExpectedDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public EventStatus Status { get; set; }
        public bool IsActive { get; set; }
    }
}
