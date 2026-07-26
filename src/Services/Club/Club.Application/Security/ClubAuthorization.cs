using Club.Application.Interfaces;
using Club.Domain.Enums;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace Club.Application.Security;

public static class ClubAuthorization
{
    public static async Task EnsureClubLeaderOrAdminAsync(
        IClubRepository clubs,
        Guid clubId,
        Guid actorId,
        string actorRole)
    {
        if (actorRole == SystemRoleNames.StudentAffairsAdmin)
        {
            return;
        }

        var membership = await clubs.GetMemberAsync(clubId, actorId);
        if (membership is null ||
            membership.Status != MembershipStatus.Approved ||
            !membership.IsActive ||
            membership.Role != ClubRole.ClubLeader)
        {
            throw new ForbiddenException("Actor is not the ClubLeader of this club.");
        }
    }
}
