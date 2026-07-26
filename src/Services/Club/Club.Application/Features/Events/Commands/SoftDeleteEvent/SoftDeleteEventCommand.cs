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
        public Guid ActorId { get; set; }
        public string ActorRole { get; set; } = string.Empty;

        public SoftDeleteEventCommand(Guid id, Guid actorId, string actorRole)
        {
            Id = id;
            ActorId = actorId;
            ActorRole = actorRole;
        }
    }
}
