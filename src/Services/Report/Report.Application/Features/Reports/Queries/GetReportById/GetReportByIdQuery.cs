using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Report.Application.Features.Reports.Queries.GetReportById
{
    public class GetReportByIdQuery : IRequest<ReportDto>
    {
        public Guid Id { get; set; }
    }

    public class GetReportByIdQueryHandler : IRequestHandler<GetReportByIdQuery, ReportDto>
    {
        private readonly IReportUnitOfWork _uow;

        public GetReportByIdQueryHandler(IReportUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ReportDto> Handle(GetReportByIdQuery request, CancellationToken cancellationToken)
        {
            var report = await _uow.Reports.GetByIdAsync(request.Id);

            if (report == null)
            {
                throw new NotFoundException($"Report with ID {request.Id} not found.");
            }

            return new ReportDto
            {
                Id = report.Id,
                Title = report.Title,
                Content = report.Content,
                Type = report.Type.ToString(),
                Status = report.Status.ToString(),
                ClubId = report.ClubId,
                SemesterId = report.SemesterId,
                CreatedBy = report.CreatedBy,
                ReviewedBy = report.ReviewedBy,
                ReviewNote = report.ReviewNote,
                RevisionNumber = report.RevisionNumber,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt,
                Attachments = report.Attachments?.Select(a => new ReportAttachmentDto
                {
                    Id = a.Id,
                    Url = a.Url,
                    FileName = a.FileName
                }).ToList() ?? new System.Collections.Generic.List<ReportAttachmentDto>()
            };
        }
    }
}
