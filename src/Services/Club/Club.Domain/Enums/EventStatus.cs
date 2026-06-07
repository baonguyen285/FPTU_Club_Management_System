using System;

namespace Club.Domain.Enums
{
    public enum EventStatus
    {
        Draft = 0,
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3,
        Completed = 4,
        Cancelled = 5
    }
}
