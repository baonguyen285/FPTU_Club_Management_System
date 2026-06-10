using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Club.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Club.Application.Features.Clubs.Commands.DeleteClub
{
    public class DeleteClubCommandHandler : IRequestHandler<DeleteClubCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteClubCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(DeleteClubCommand request, CancellationToken cancellationToken)
        {
            var club = await _unitOfWork.Clubs.GetByIdAsync(request.Id);
            if (club == null)
                throw new NotFoundException($"Club with ID '{request.Id}' was not found.");

            // Xóa mềm: chỉ set IsActive = false, giữ lại lịch sử
            club.IsActive = false;
            club.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Clubs.Update(club);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
