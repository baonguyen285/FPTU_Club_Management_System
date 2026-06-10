using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;

namespace Club.Application.Features.Clubs.Queries.GetClubs
{
    public class GetClubsQueryHandler : IRequestHandler<GetClubsQuery, IEnumerable<ClubDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetClubsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ClubDto>> Handle(GetClubsQuery request, CancellationToken cancellationToken)
        {
            var clubs = await _unitOfWork.Clubs.GetAllAsync();
            return _mapper.Map<IEnumerable<ClubDto>>(clubs);
        }
    }
}
