using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Domain.Enums;

namespace Report.Application.Features.Reports.Queries.GetReportsByClub
{
    public class GetReportsByClubQuery : IRequest<IEnumerable<ReportDto>>
    {
        public Guid ClubId { get; set; }
        public ReportStatus? Status { get; set; }
        public ReportType? Type { get; set; }
    }

    public class GetReportsByClubQueryHandler : IRequestHandler<GetReportsByClubQuery, IEnumerable<ReportDto>>
    {
        private readonly IReportUnitOfWork _uow;

        public GetReportsByClubQueryHandler(IReportUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<IEnumerable<ReportDto>> Handle(GetReportsByClubQuery request, CancellationToken cancellationToken)
        {
            var reports = await _uow.Reports.GetReportsByClubAsync(request.ClubId, request.Status, request.Type);

            return reports.Select(r => new ReportDto
            {
                Id = r.Id,
                Title = r.Title,
                Content = r.Content,
                Type = r.Type.ToString(),
                Status = r.Status.ToString(),
                ClubId = r.ClubId,
                CreatedBy = r.CreatedBy,
                ReviewedBy = r.ReviewedBy,
                ReviewNote = r.ReviewNote,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            });
        }
    }
}
