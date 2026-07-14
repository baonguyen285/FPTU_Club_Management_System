using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using MediatR;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Events.Queries.GetEventById
{
    public class GetEventByIdQueryHandler : IRequestHandler<GetEventByIdQuery, EventDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetEventByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<EventDto> Handle(GetEventByIdQuery request, CancellationToken cancellationToken)
        {
            var clubEvent = await _unitOfWork.Clubs.GetEventByIdAsync(request.Id);
            if (clubEvent == null)
            {
                throw new NotFoundException("Event not found.");
            }

            return _mapper.Map<EventDto>(clubEvent);
        }
    }
}
