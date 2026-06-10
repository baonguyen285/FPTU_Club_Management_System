using System;
using MediatR;

namespace Club.Application.Features.Events.Commands.SoftDeleteEvent
{
    /// <summary>
    /// Xóa mềm sự kiện: set IsActive = false, giữ lại lịch sử.
    /// Dùng cho trường hợp hủy sự kiện.
    /// </summary>
    public class SoftDeleteEventCommand : IRequest<bool>
    {
        public Guid Id { get; set; }

        public SoftDeleteEventCommand(Guid id)
        {
            Id = id;
        }
    }
}
