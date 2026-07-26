using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Club.Application.Interfaces;
using Club.Domain.Enums;
using Shared.Kernel.Exceptions;
using Club.Application.Security;

namespace Club.Application.Features.Events.Commands.SoftDeleteEvent
{
    public class SoftDeleteEventCommandHandler : IRequestHandler<SoftDeleteEventCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public SoftDeleteEventCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(SoftDeleteEventCommand request, CancellationToken cancellationToken)
        {
            var ev = await _unitOfWork.Clubs.GetEventByIdAsync(request.Id);
            if (ev == null)
                throw new NotFoundException($"Event with ID '{request.Id}' was not found.");
            await ClubAuthorization.EnsureClubLeaderOrAdminAsync(
                _unitOfWork.Clubs, ev.ClubId, request.ActorId, request.ActorRole);
            if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
                throw new ConflictException($"Event in {ev.Status} status cannot be cancelled.");

            // Xóa mềm: set IsActive = false và đổi Status sang Cancelled
            ev.IsActive = false;
            ev.Status = EventStatus.Cancelled;
            ev.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Clubs.UpdateEvent(ev);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
