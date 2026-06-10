using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Clubs.Commands.UpdateClub
{
    public class UpdateClubCommandHandler : IRequestHandler<UpdateClubCommand, ClubDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public UpdateClubCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ClubDto> Handle(UpdateClubCommand request, CancellationToken cancellationToken)
        {
            var club = await _unitOfWork.Clubs.GetByIdAsync(request.Id);
            if (club == null)
                throw new NotFoundException($"Club with ID '{request.Id}' was not found.");

            club.Name = request.Name;
            club.Description = request.Description;
            club.LogoUrl = request.LogoUrl;
            club.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Clubs.Update(club);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<ClubDto>(club);
        }
    }
}
