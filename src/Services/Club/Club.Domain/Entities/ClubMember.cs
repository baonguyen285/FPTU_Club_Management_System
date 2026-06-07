using System;
using Shared.Kernel.Domain;
using Club.Domain.Enums;

namespace Club.Domain.Entities
{
    public class ClubMember : BaseEntity
    {
        public Guid ClubId { get; set; }
        public Guid UserId { get; set; }
        public ClubRole Role { get; set; } = ClubRole.Member;
        public MembershipStatus Status { get; set; } = MembershipStatus.Pending;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public Club Club { get; set; } = null!;
    }
}
