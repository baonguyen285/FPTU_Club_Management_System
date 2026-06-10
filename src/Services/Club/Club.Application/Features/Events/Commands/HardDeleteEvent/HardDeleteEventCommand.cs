using System;
using MediatR;

namespace Club.Application.Features.Events.Commands.HardDeleteEvent
{
    /// <summary>
    /// Xóa vĩnh viễn sự kiện khỏi Database.
    /// Dùng cho trường hợp sự kiện tạo nhầm hoặc dọn dẹp dữ liệu rác.
    /// </summary>
    public class HardDeleteEventCommand : IRequest<bool>
    {
        public Guid Id { get; set; }

        public HardDeleteEventCommand(Guid id)
        {
            Id = id;
        }
    }
}
