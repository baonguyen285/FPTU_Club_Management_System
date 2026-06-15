using System;
using System.ComponentModel.DataAnnotations;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Clubs.Commands.CreateClub
{
    public class CreateClubCommand : IRequest<ClubDto>
    {
        [Required(ErrorMessage = "Club name is required")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Club name must be between 3 and 150 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Club description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;

        [Url(ErrorMessage = "Logo URL must be a valid URL")]
        public string? LogoUrl { get; set; }

        [Required(ErrorMessage = "Advisor ID is required")]
        public Guid AdvisorId { get; set; }
    }
}
