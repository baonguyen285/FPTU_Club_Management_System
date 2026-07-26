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
        public Guid SemesterId { get; set; }
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
                throw new ForbiddenException("You do not have permission to submit reports for this club.");
            }

            if (request.SemesterId == Guid.Empty)
                throw new BadRequestException("SemesterId is required for new reports.");

            var semester = await _uow.GetSemesterAsync(request.SemesterId, cancellationToken)
                ?? throw new NotFoundException("Semester not found.");
            if (semester.Status != SemesterStatus.Active)
                throw new ConflictException("Only an active semester accepts new reports.");

            var report = new Domain.Entities.Report(
                request.ClubId,
                request.SemesterId,
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
                SemesterId = report.SemesterId,
                CreatedBy = report.CreatedBy,
                CreatedAt = report.CreatedAt,
                RevisionNumber = report.RevisionNumber,
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
