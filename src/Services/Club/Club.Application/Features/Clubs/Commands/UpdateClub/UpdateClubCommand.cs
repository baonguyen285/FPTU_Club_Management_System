using System;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Clubs.Commands.UpdateClub
{
    public class UpdateClubCommand : IRequest<ClubDto>
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }
}
