using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Club.Domain.Enums;

namespace Club.Application.Features.Clubs.Commands.CreateClub
{
    public class CreateClubCommandHandler : IRequestHandler<CreateClubCommand, ClubDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CreateClubCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ClubDto> Handle(CreateClubCommand request, CancellationToken cancellationToken)
        {
            var club = new Domain.Entities.Club
            {
                Name = request.Name,
                Description = request.Description,
                LogoUrl = request.LogoUrl,
                AdvisorId = request.AdvisorId,
                Status = ClubStatus.PendingApproval,
                IsActive = true
            };

            await _unitOfWork.Clubs.AddAsync(club);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<ClubDto>(club);
        }
    }
}
