using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;
using Club.Domain.Enums;
using Club.Application.Security;

namespace Club.Application.Features.Members.Commands.UpdateMemberRole
{
    public class UpdateMemberRoleCommandHandler : IRequestHandler<UpdateMemberRoleCommand, ClubMemberDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public UpdateMemberRoleCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ClubMemberDto> Handle(UpdateMemberRoleCommand request, CancellationToken cancellationToken)
        {
            await ClubAuthorization.EnsureClubLeaderOrAdminAsync(
                _unitOfWork.Clubs, request.ClubId, request.ActorId, request.ActorRole);
            if (request.NewRole == ClubRole.LegacyManager)
                throw new ConflictException("LegacyManager cannot be assigned to new memberships.");

            var member = await _unitOfWork.Clubs.GetMemberAsync(request.ClubId, request.UserId);
            if (member == null)
                throw new NotFoundException($"Member with UserId '{request.UserId}' in Club '{request.ClubId}' was not found.");
            if (member.Role == ClubRole.ClubLeader && request.NewRole != ClubRole.ClubLeader)
            {
                var members = await _unitOfWork.Clubs.GetMembersByClubAsync(request.ClubId);
                if (members.Count(x => x.Role == ClubRole.ClubLeader &&
                    x.Status == MembershipStatus.Approved && x.IsActive) <= 1)
                    throw new ConflictException("The last active ClubLeader cannot be demoted.");
            }

            member.Role = request.NewRole;
            member.Status = request.NewStatus;
            member.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Clubs.UpdateMember(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<ClubMemberDto>(member);
        }
    }
}
