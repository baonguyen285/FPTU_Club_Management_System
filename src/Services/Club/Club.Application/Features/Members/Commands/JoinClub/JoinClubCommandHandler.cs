using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Club.Domain.Enums;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Members.Commands.JoinClub
{
    public class JoinClubCommandHandler : IRequestHandler<JoinClubCommand, ClubMemberDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public JoinClubCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ClubMemberDto> Handle(JoinClubCommand request, CancellationToken cancellationToken)
        {
            var club = await _unitOfWork.Clubs.GetByIdAsync(request.ClubId);
            if (club == null)
            {
                throw new NotFoundException($"Club with ID {request.ClubId} not found.");
            }

            var existingMember = await _unitOfWork.Clubs.GetMemberAsync(request.ClubId, request.UserId);
            if (existingMember != null)
            {
                throw new BadRequestException("User has already joined or applied to this club.");
            }

            var member = new Domain.Entities.ClubMember
            {
                ClubId = request.ClubId,
                UserId = request.UserId,
                Role = ClubRole.Member,
                Status = MembershipStatus.Pending,
                JoinedAt = System.DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.Clubs.AddMemberAsync(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<ClubMemberDto>(member);
        }
    }
}
