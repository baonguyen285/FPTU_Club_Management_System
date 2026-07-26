using System;

namespace Club.Domain.Enums
{
    public enum ClubRole
    {
        Member = 0,
        [Obsolete("Legacy Manager records require an explicit per-record migration decision.")]
        LegacyManager = 1,
        ClubLeader = 2,
        Treasurer = 3
    }
}
