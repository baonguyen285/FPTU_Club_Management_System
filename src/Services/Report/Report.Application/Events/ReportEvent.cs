using System;

namespace Report.Application.Events
{
    public record ReportEvent(Guid ReportId, Guid ClubId, Guid UserId, string EventType, DateTime Timestamp);
}