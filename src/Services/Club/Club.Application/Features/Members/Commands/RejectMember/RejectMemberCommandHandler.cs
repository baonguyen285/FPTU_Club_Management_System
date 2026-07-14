using System.Threading;
using System.Threading.Tasks;
using Club.Application.Interfaces;
using Club.Domain.Enums;
using MediatR;
using Shared.Kernel.Exceptions;

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
            var member = await _unitOfWork.Clubs.GetMemberAsync(request.ClubId, request.UserId);
            if (member == null)
            {
                throw new NotFoundException("Membership request not found.");
            }

            member.Status = MembershipStatus.Rejected;
            member.IsActive = false;
            member.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Clubs.UpdateMember(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
