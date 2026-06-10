using System;
using Club.Domain.Enums;

namespace Club.Application.DTOs
{
    public class ClubDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public Guid AdvisorId { get; set; }
        public ClubStatus Status { get; set; }
        public bool IsActive { get; set; }
    }
}
