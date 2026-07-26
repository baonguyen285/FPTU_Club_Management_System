using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Club.Domain.Enums;
using Shared.Kernel.Exceptions;
using Club.Application.Security;

namespace Club.Application.Features.Events.Commands.CreateEvent
{
    public class CreateEventCommandHandler : IRequestHandler<CreateEventCommand, EventDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IClubEventPublisher _eventPublisher;

        public CreateEventCommandHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IClubEventPublisher eventPublisher)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _eventPublisher = eventPublisher;
        }

        public async Task<EventDto> Handle(CreateEventCommand request, CancellationToken cancellationToken)
        {
            var club = await _unitOfWork.Clubs.GetByIdAsync(request.ClubId);
            if (club == null)
            {
                throw new NotFoundException($"Club with ID {request.ClubId} not found.");
            }
            await ClubAuthorization.EnsureClubLeaderOrAdminAsync(
                _unitOfWork.Clubs, request.ClubId, request.ActorId, request.ActorRole);
            if (!club.IsActive || club.Status != ClubStatus.Active)
                throw new ConflictException("Activities can only be created for active clubs.");
            if (request.ExpectedDate <= System.DateTime.UtcNow)
                throw new BadRequestException("Expected date must be in the future.");

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
            await _eventPublisher.PublishActivityCreatedAsync(newEvent, cancellationToken);

            return _mapper.Map<EventDto>(newEvent);
        }
    }
}
