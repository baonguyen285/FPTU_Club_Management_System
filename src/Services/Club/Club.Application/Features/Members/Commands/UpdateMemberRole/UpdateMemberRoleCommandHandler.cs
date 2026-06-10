using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Shared.Kernel.Exceptions;

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
            var member = await _unitOfWork.Clubs.GetMemberAsync(request.ClubId, request.UserId);
            if (member == null)
                throw new NotFoundException($"Member with UserId '{request.UserId}' in Club '{request.ClubId}' was not found.");

            member.Role = request.NewRole;
            member.Status = request.NewStatus;
            member.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Clubs.UpdateMember(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<ClubMemberDto>(member);
        }
    }
}
