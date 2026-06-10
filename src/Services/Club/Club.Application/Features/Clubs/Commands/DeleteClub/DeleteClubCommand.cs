using System;
using MediatR;

namespace Club.Application.Features.Clubs.Commands.DeleteClub
{
    /// <summary>
    /// Xóa mềm CLB (set IsActive = false). Không xóa vĩnh viễn.
    /// </summary>
    public class DeleteClubCommand : IRequest<bool>
    {
        public Guid Id { get; set; }

        public DeleteClubCommand(Guid id)
        {
            Id = id;
        }
    }
}
