using System;
using Club.Domain.Enums;

namespace Club.Application.DTOs
{
    public class ClubMemberDto
    {
        public Guid Id { get; set; }
        public Guid ClubId { get; set; }
        public Guid UserId { get; set; }
        public ClubRole Role { get; set; }
        public MembershipStatus Status { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
