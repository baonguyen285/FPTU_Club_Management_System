using System;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Clubs.Commands.CreateClub
{
    public class CreateClubCommand : IRequest<ClubDto>
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public Guid AdvisorId { get; set; }
    }
}
