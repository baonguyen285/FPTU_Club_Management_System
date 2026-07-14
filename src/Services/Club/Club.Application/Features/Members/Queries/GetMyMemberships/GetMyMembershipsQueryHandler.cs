using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using MediatR;

namespace Club.Application.Features.Members.Queries.GetMyMemberships
{
    public class GetMyMembershipsQueryHandler : IRequestHandler<GetMyMembershipsQuery, IEnumerable<ClubMemberDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetMyMembershipsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ClubMemberDto>> Handle(GetMyMembershipsQuery request, CancellationToken cancellationToken)
        {
            var memberships = await _unitOfWork.Clubs.GetMembersByUserAsync(request.UserId);
            return _mapper.Map<IEnumerable<ClubMemberDto>>(memberships);
        }
    }
}
