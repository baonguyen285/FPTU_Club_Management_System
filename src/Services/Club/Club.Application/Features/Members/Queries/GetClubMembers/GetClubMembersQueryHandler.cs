using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Members.Queries.GetClubMembers
{
    public class GetClubMembersQueryHandler : IRequestHandler<GetClubMembersQuery, IEnumerable<ClubMemberDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetClubMembersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ClubMemberDto>> Handle(GetClubMembersQuery request, CancellationToken cancellationToken)
        {
            var club = await _unitOfWork.Clubs.GetByIdAsync(request.ClubId);
            if (club == null)
                throw new NotFoundException($"Club with ID '{request.ClubId}' was not found.");

            var members = await _unitOfWork.Clubs.GetMembersByClubAsync(request.ClubId);
            return _mapper.Map<IEnumerable<ClubMemberDto>>(members);
        }
    }
}
