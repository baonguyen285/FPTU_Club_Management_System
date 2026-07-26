using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Club.Application.Interfaces;
using Club.Domain.Enums;
using Shared.Kernel.Exceptions;
using Club.Application.Security;

namespace Club.Application.Features.Members.Commands.RemoveMember
{
    public class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public RemoveMemberCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
        {
            await ClubAuthorization.EnsureClubLeaderOrAdminAsync(
                _unitOfWork.Clubs, request.ClubId, request.ActorId, request.ActorRole);
            var member = await _unitOfWork.Clubs.GetMemberAsync(request.ClubId, request.UserId);
            if (member == null)
                throw new NotFoundException($"Member with UserId '{request.UserId}' in Club '{request.ClubId}' was not found.");
            if (member.Status != MembershipStatus.Approved)
                throw new ConflictException($"Membership in {member.Status} status cannot be removed.");
            if (member.Role == ClubRole.ClubLeader)
            {
                var members = await _unitOfWork.Clubs.GetMembersByClubAsync(request.ClubId);
                if (members.Count(x => x.Role == ClubRole.ClubLeader &&
                    x.Status == MembershipStatus.Approved && x.IsActive) <= 1)
                    throw new ConflictException("The last active ClubLeader cannot be removed.");
            }

            // Đổi trạng thái sang Left thay vì xóa record
            member.Status = MembershipStatus.Left;
            member.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Clubs.UpdateMember(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
