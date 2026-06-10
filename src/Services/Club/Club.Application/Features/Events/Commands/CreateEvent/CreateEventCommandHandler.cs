using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Club.Domain.Enums;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Events.Commands.CreateEvent
{
    public class CreateEventCommandHandler : IRequestHandler<CreateEventCommand, EventDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CreateEventCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<EventDto> Handle(CreateEventCommand request, CancellationToken cancellationToken)
        {
            var club = await _unitOfWork.Clubs.GetByIdAsync(request.ClubId);
            if (club == null)
            {
                throw new NotFoundException($"Club with ID {request.ClubId} not found.");
            }

            var newEvent = new Domain.Entities.Event
            {
                ClubId = request.ClubId,
                Title = request.Title,
                Description = request.Description,
                ExpectedDate = request.ExpectedDate,
                Location = request.Location,
                Status = EventStatus.Draft,
                IsActive = true
            };

            await _unitOfWork.Clubs.AddEventAsync(newEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<EventDto>(newEvent);
        }
    }
}
