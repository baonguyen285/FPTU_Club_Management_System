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
        private readonly IClubGrpcClient _grpcClient;

        public CreateReportCommandHandler(IReportUnitOfWork uow, IClubGrpcClient grpcClient)
        {
            _uow = uow;
            _grpcClient = grpcClient;
        }

        public async Task<ReportDto> Handle(CreateReportCommand request, CancellationToken cancellationToken)
        {
            // Verify if club exists via gRPC
            var clubExists = await _grpcClient.CheckClubExistsAsync(request.ClubId, cancellationToken);
            if (!clubExists)
            {
                throw new NotFoundException($"Club with ID {request.ClubId} does not exist.");
            }

            // Verify if creator is the manager/president of the club via gRPC
            bool isManager = await _grpcClient.CanSubmitReportsAsync(request.ClubId, request.CreatedBy, cancellationToken);
            if (!isManager)
            {
                throw new UnauthorizedException("You do not have permission to submit reports for this club.");
            }

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
            var submittedAt = report.CreatedAt;
            var payload = new Shared.Kernel.IntegrationEvents.IntegrationEventEnvelopeV1(
                Guid.NewGuid(), "ReportSubmittedV1", "v1", submittedAt, "report-service", Guid.NewGuid().ToString("N"),
                new Shared.Kernel.IntegrationEvents.ReportSubmittedV1(report.Id, report.ClubId, report.CreatedBy, report.Type.ToString(), "Unspecified", submittedAt));
            var legacy = new Events.ReportSubmittedEvent(report.Id, report.ClubId, report.Title, report.CreatedBy, submittedAt);
            await _uow.AddOutboxMessageAsync(new OutboxMessage(
                payload.EventType,
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Json.JsonSerializer.Serialize(legacy),
                submittedAt), cancellationToken);
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
