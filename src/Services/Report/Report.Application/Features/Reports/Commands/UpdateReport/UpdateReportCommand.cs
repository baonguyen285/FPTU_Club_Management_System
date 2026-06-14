using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Domain.Enums;
using Shared.Kernel.Exceptions;

namespace Report.Application.Features.Reports.Commands.UpdateReport
{
    public class UpdateReportCommand : IRequest<ReportDto>
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public ReportType Type { get; set; }
        public Guid UserId { get; set; }
    }

    public class UpdateReportCommandHandler : IRequestHandler<UpdateReportCommand, ReportDto>
    {
        private readonly IReportUnitOfWork _uow;

        public UpdateReportCommandHandler(IReportUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ReportDto> Handle(UpdateReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _uow.Reports.GetByIdAsync(request.Id);

            if (report == null)
            {
                throw new NotFoundException($"Report with ID {request.Id} not found.");
            }

            if (report.CreatedBy != request.UserId)
            {
                throw new UnauthorizedException("You are not allowed to update this report.");
            }

            report.Update(request.Title, request.Content, request.Type);

            _uow.Reports.Update(report);
            await _uow.SaveChangesAsync();

            return new ReportDto
            {
                Id = report.Id,
                Title = report.Title,
                Content = report.Content,
                Type = report.Type.ToString(),
                Status = report.Status.ToString(),
                ClubId = report.ClubId,
                CreatedBy = report.CreatedBy,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt
            };
        }
    }
}
