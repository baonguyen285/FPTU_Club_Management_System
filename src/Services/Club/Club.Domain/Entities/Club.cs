using System;
using Club.Domain.Enums;
using Shared.Kernel.Domain;

namespace Club.Domain.Entities
{
    public class Club : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public Guid AdvisorId { get; set; }
        public ClubStatus Status { get; set; } = ClubStatus.PendingApproval;
        public ICollection<ClubMember> Members { get; set; } = new List<ClubMember>();
        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}
