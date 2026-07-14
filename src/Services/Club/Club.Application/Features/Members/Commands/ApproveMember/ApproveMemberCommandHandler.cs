using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Club.Domain.Enums;
using MediatR;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Members.Commands.ApproveMember
{
    public class ApproveMemberCommandHandler : IRequestHandler<ApproveMemberCommand, ClubMemberDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ApproveMemberCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ClubMemberDto> Handle(ApproveMemberCommand request, CancellationToken cancellationToken)
        {
            var member = await _unitOfWork.Clubs.GetMemberAsync(request.ClubId, request.UserId);
            if (member == null)
            {
                throw new NotFoundException("Membership request not found.");
            }

            member.Status = MembershipStatus.Approved;
            member.IsActive = true;
            member.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Clubs.UpdateMember(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<ClubMemberDto>(member);
        }
    }
}
