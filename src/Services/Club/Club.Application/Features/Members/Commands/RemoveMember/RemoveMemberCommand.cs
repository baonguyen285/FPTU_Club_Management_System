using System;
using MediatR;

namespace Club.Application.Features.Members.Commands.RemoveMember
{
    /// <summary>
    /// Đuổi thành viên hoặc tự rời CLB.
    /// Đổi MembershipStatus sang Left thay vì xóa record.
    /// </summary>
    public class RemoveMemberCommand : IRequest<bool>
    {
        public Guid ClubId { get; set; }
        public Guid UserId { get; set; }
        public Guid ActorId { get; set; }
        public string ActorRole { get; set; } = string.Empty;

        public RemoveMemberCommand(Guid clubId, Guid userId, Guid actorId, string actorRole)
        {
            ClubId = clubId;
            UserId = userId;
            ActorId = actorId;
            ActorRole = actorRole;
        }
    }
}
