using System.Threading;
using System.Threading.Tasks;
using Club.Application.Interfaces;
using Club.Domain.Enums;
using MediatR;
using Shared.Kernel.Exceptions;
using Club.Application.Security;

namespace Club.Application.Features.Members.Commands.RejectMember
{
    public class RejectMemberCommandHandler : IRequestHandler<RejectMemberCommand>
    {
        private readonly IUnitOfWork _unitOfWork;

        public RejectMemberCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(RejectMemberCommand request, CancellationToken cancellationToken)
        {
            await ClubAuthorization.EnsureClubLeaderOrAdminAsync(
                _unitOfWork.Clubs, request.ClubId, request.ActorId, request.ActorRole);
            var member = await _unitOfWork.Clubs.GetMemberAsync(request.ClubId, request.UserId);
            if (member == null)
            {
                throw new NotFoundException("Membership request not found.");
            }
            if (member.Status != MembershipStatus.Pending)
            {
                throw new ConflictException($"Membership cannot transition from {member.Status} to Rejected.");
            }

            member.Status = MembershipStatus.Rejected;
            member.IsActive = false;
            member.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Clubs.UpdateMember(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
