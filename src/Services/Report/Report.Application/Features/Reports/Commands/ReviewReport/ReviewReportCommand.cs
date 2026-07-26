using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;
using Report.Domain.Enums;

namespace Report.Application.Features.Reports.Commands.ReviewReport
{
    public class ReviewReportCommand : IRequest<ReportDto>
    {
        public Guid ReportId { get; set; }
        public Guid UserId { get; set; }
        public string ActorRole { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public string? Action { get; set; }
        public string? ReviewNote { get; set; }
    }

    public class ReviewReportCommandHandler : IRequestHandler<ReviewReportCommand, ReportDto>
    {
        private readonly IReportUnitOfWork _uow;

        public ReviewReportCommandHandler(IReportUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ReportDto> Handle(ReviewReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _uow.Reports.GetByIdAsync(request.ReportId);

            if (report == null)
            {
                throw new NotFoundException($"Report with ID {request.ReportId} not found.");
            }

            if (!string.Equals(request.ActorRole, SystemRoleNames.StudentAffairsAdmin, StringComparison.Ordinal))
            {
                throw new ForbiddenException("Only StudentAffairsAdmin can review reports.");
            }

            if (report.Status != ReportStatus.PendingApproval)
                throw new ConflictException("Only pending reports can be reviewed.");

            var action = string.IsNullOrWhiteSpace(request.Action)
                ? (request.IsApproved ? "Approve" : "Reject")
                : request.Action.Trim();
            if ((action.Equals("Reject", StringComparison.OrdinalIgnoreCase)
                || action.Equals("RequestRevision", StringComparison.OrdinalIgnoreCase))
                && string.IsNullOrWhiteSpace(request.ReviewNote))
                throw new BadRequestException("Review feedback is required for rejection or revision.");

            var previous = report.Status;
            if (action.Equals("Approve", StringComparison.OrdinalIgnoreCase))
            {
                report.Approve(request.UserId, request.ReviewNote);
            }
            else if (action.Equals("RequestRevision", StringComparison.OrdinalIgnoreCase))
            {
                report.RequestRevision(request.UserId, request.ReviewNote!);
            }
            else if (action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
            {
                report.Reject(request.UserId, request.ReviewNote!);
            }
            else
            {
                throw new BadRequestException("Action must be Approve, RequestRevision, or Reject.");
            }

            await _uow.AddHistoryAsync(new Report.Domain.Entities.ReportRevisionHistory(
                report.Id, report.RevisionNumber, previous, report.Status, request.ReviewNote, request.UserId), cancellationToken);
            var reviewedAt = DateTime.UtcNow;
            var payload = new Shared.Kernel.IntegrationEvents.IntegrationEventEnvelopeV1(
                Guid.NewGuid(), "ReportReviewedV1", "v1", reviewedAt, "report-service", Guid.NewGuid().ToString("N"),
                new Shared.Kernel.IntegrationEvents.ReportWorkflowEventV1(
                    report.Id, report.ClubId, report.SemesterId ?? Guid.Empty, report.Status.ToString(),
                    report.RevisionNumber, request.UserId, request.ReviewNote));
            await _uow.AddOutboxMessageAsync(new Report.Domain.Entities.OutboxMessage(
                payload.EventType, System.Text.Json.JsonSerializer.Serialize(payload), null, reviewedAt), cancellationToken);
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
                SemesterId = report.SemesterId,
                CreatedBy = report.CreatedBy,
                ReviewedBy = report.ReviewedBy,
                ReviewNote = report.ReviewNote,
                RevisionNumber = report.RevisionNumber,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt
            };
        }
    }
}
