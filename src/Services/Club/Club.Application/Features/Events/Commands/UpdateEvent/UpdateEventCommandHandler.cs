using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Events.Commands.UpdateEvent
{
    public class UpdateEventCommandHandler : IRequestHandler<UpdateEventCommand, EventDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public UpdateEventCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<EventDto> Handle(UpdateEventCommand request, CancellationToken cancellationToken)
        {
            var ev = await _unitOfWork.Clubs.GetEventByIdAsync(request.Id);
            if (ev == null)
                throw new NotFoundException($"Event with ID '{request.Id}' was not found.");

            ev.Title = request.Title;
            ev.Description = request.Description;
            ev.ExpectedDate = request.ExpectedDate;
            ev.Location = request.Location;
            ev.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Clubs.UpdateEvent(ev);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<EventDto>(ev);
        }
    }
}
