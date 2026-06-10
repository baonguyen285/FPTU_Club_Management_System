using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Clubs.Queries.GetClubById
{
    public class GetClubByIdQueryHandler : IRequestHandler<GetClubByIdQuery, ClubDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetClubByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ClubDto> Handle(GetClubByIdQuery request, CancellationToken cancellationToken)
        {
            var club = await _unitOfWork.Clubs.GetByIdAsync(request.Id);
            if (club == null)
                throw new NotFoundException($"Club with ID '{request.Id}' was not found.");

            return _mapper.Map<ClubDto>(club);
        }
    }
}
