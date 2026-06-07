using System;
using Club.Domain.Enums;
using Shared.Kernel.Domain;

namespace Club.Domain.Entities
{
    public class Event : BaseEntity
    {
        public Guid ClubId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ExpectedDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public EventStatus Status { get; set; } = EventStatus.Draft;
        public Club Club { get; set; } = null!;
    }
}
