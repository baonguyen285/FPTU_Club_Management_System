using System;

namespace Report.Application.Events
{
    public class ReportSubmittedEvent
    {
        public Guid ReportId { get; set; }
        public Guid ClubId { get; set; }
        public string Title { get; set; }
        public Guid SubmittedBy { get; set; }
        public DateTime SubmittedAt { get; set; }

        public ReportSubmittedEvent(Guid reportId, Guid clubId, string title, Guid submittedBy, DateTime submittedAt)
        {
            ReportId = reportId;
            ClubId = clubId;
            Title = title;
            SubmittedBy = submittedBy;
            SubmittedAt = submittedAt;
        }
    }
}
