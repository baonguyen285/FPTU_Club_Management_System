using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;

namespace Club.Application.Features.Events.Queries.GetEventsByClub
{
    public class GetEventsByClubQueryHandler : IRequestHandler<GetEventsByClubQuery, IEnumerable<EventDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetEventsByClubQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<EventDto>> Handle(GetEventsByClubQuery request, CancellationToken cancellationToken)
        {
            var events = await _unitOfWork.Clubs.GetEventsByClubAsync(request.ClubId);
            return _mapper.Map<IEnumerable<EventDto>>(events);
        }
    }
}
