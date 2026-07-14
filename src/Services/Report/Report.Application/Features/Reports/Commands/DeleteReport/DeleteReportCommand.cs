using System;
using MediatR;

namespace Report.Application.Features.Reports.Commands.DeleteReport
{
    public class DeleteReportCommand : IRequest
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
