using System;
using MediatR;

namespace Report.Application.Features.Reports.Commands.SubmitReport
{
    public class SubmitReportCommand : IRequest<bool>
    {
        public Guid ReportId { get; set; }
        public Guid ClubId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public Guid UserId { get; set; }

        public SubmitReportCommand(Guid reportId, Guid clubId, string title, string content, Guid userId)
        {
            ReportId = reportId;
            ClubId = clubId;
            Title = title;
            Content = content;
            UserId = userId;
        }
    }
}
