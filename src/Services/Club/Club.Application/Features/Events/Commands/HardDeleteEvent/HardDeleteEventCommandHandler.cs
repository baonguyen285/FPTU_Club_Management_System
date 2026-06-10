using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Club.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Events.Commands.HardDeleteEvent
{
    public class HardDeleteEventCommandHandler : IRequestHandler<HardDeleteEventCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public HardDeleteEventCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(HardDeleteEventCommand request, CancellationToken cancellationToken)
        {
            var ev = await _unitOfWork.Clubs.GetEventByIdAsync(request.Id);
            if (ev == null)
                throw new NotFoundException($"Event with ID '{request.Id}' was not found.");

            // Xóa vĩnh viễn khỏi Database
            _unitOfWork.Clubs.DeleteEvent(ev);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
