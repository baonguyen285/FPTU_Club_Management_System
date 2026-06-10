using System;
using MediatR;
using Club.Application.DTOs;

namespace Club.Application.Features.Clubs.Queries.GetClubById
{
    public class GetClubByIdQuery : IRequest<ClubDto>
    {
        public Guid Id { get; set; }

        public GetClubByIdQuery(Guid id)
        {
            Id = id;
        }
    }
}
