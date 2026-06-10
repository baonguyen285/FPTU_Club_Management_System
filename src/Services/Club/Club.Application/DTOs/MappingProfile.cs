using AutoMapper;
using Club.Domain.Entities;

namespace Club.Application.DTOs
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Domain.Entities.Club, ClubDto>().ReverseMap();
            CreateMap<Event, EventDto>().ReverseMap();
            CreateMap<ClubMember, ClubMemberDto>().ReverseMap();
        }
    }
}
