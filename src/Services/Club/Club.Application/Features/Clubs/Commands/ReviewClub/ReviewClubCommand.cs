using System;
using System.ComponentModel.DataAnnotations;
using MediatR;
using Club.Application.DTOs;
using Club.Domain.Enums;

namespace Club.Application.Features.Clubs.Commands.ReviewClub
{
    public class ReviewClubCommand : IRequest<ClubDto>
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [EnumDataType(typeof(ClubStatus), ErrorMessage = "Invalid ClubStatus value")]
        public ClubStatus Status { get; set; }
    }
}
