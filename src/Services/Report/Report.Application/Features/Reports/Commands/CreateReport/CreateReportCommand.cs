using System;
using System.Collections.Generic;
using System.Linq;
using MediatR;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Domain.Entities;
using Report.Domain.Enums;
using Shared.Kernel.Exceptions;

namespace Report.Application.Features.Reports.Commands.CreateReport
{
    public class CreateReportCommand : IRequest<ReportDto>
    {
        public Guid ClubId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public ReportType Type { get; set; }
        public Guid CreatedBy { get; set; }
        public List<AttachmentInput>? Attachments { get; set; }
    }

    public class AttachmentInput
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class CreateReportCommandHandler : IRequestHandler<CreateReportCommand, ReportDto>
    {
        private readonly IReportUnitOfWork _uow;

        public CreateReportCommandHandler(IReportUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ReportDto> Handle(CreateReportCommand request, CancellationToken cancellationToken)
        {
            var report = new Domain.Entities.Report(
                request.ClubId,
                request.Title,
                request.Content,
                request.Type,
                request.CreatedBy
            );

            if (request.Attachments != null && request.Attachments.Any())
            {
                foreach (var att in request.Attachments)
                {
                    report.AddAttachment(att.Url, att.FileName);
                }
            }

            await _uow.Reports.AddAsync(report);
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
                Attachments = report.Attachments.Select(a => new ReportAttachmentDto
                {
                    Id = a.Id,
                    Url = a.Url,
                    FileName = a.FileName
                }).ToList()
            };
        }
    }
}
