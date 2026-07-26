using MediatR;
using Report.Application.DTOs;
using Report.Application.Events;
using Report.Application.Interfaces;
using Report.Domain.Entities;
using Report.Domain.Enums;
using Shared.Kernel.Exceptions;
using Shared.Kernel.IntegrationEvents;

namespace Report.Application.Features.Reports.Commands.SubmitReport;

public sealed class SubmitReportCommand : IRequest<ReportDto>
{
    public Guid ReportId { get; set; }
    public Guid UserId { get; set; }
}

public sealed class SubmitReportCommandHandler : IRequestHandler<SubmitReportCommand, ReportDto>
{
    private readonly IReportUnitOfWork _uow;
    private readonly IClubGrpcClient _club;

    public SubmitReportCommandHandler(IReportUnitOfWork uow, IClubGrpcClient club) =>
        (_uow, _club) = (uow, club);

    public async Task<ReportDto> Handle(SubmitReportCommand request, CancellationToken cancellationToken)
    {
        var report = await _uow.Reports.GetByIdAsync(request.ReportId)
            ?? throw new NotFoundException("Report not found.");
        if (!await _club.CanSubmitReportsAsync(report.ClubId, request.UserId, cancellationToken))
            throw new ForbiddenException("You do not have permission to submit reports for this club.");
        if (!report.SemesterId.HasValue)
            throw new ConflictException("Legacy reports without a semester cannot be submitted.");

        var semester = await _uow.GetSemesterAsync(report.SemesterId.Value, cancellationToken)
            ?? throw new NotFoundException("Semester not found.");
        if (semester.Status != SemesterStatus.Active)
            throw new ConflictException("Only an active semester accepts report submissions.");

        var previous = report.Status;
        report.Submit();
        await _uow.AddHistoryAsync(new ReportRevisionHistory(
            report.Id, report.RevisionNumber, previous, report.Status, null, request.UserId), cancellationToken);

        var submittedAt = DateTime.UtcNow;
        var eventType = previous == ReportStatus.RequestRevision ? "ReportResubmittedV1" : "ReportSubmittedV1";
        var payload = new IntegrationEventEnvelopeV1(
            Guid.NewGuid(), eventType, "v1", submittedAt, "report-service", Guid.NewGuid().ToString("N"),
            new ReportWorkflowEventV1(report.Id, report.ClubId, report.SemesterId.Value, report.Status.ToString(), report.RevisionNumber, request.UserId, null));
        var legacy = new ReportSubmittedEvent(report.Id, report.ClubId, report.Title, report.CreatedBy, submittedAt);
        await _uow.AddOutboxMessageAsync(new OutboxMessage(
            payload.EventType,
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Json.JsonSerializer.Serialize(legacy),
            submittedAt), cancellationToken);
        _uow.Reports.Update(report);
        await _uow.SaveChangesAsync();

        return new ReportDto
        {
            Id = report.Id, ClubId = report.ClubId, SemesterId = report.SemesterId,
            Title = report.Title, Content = report.Content, Type = report.Type.ToString(),
            Status = report.Status.ToString(), CreatedBy = report.CreatedBy,
            ReviewNote = report.ReviewNote, RevisionNumber = report.RevisionNumber,
            CreatedAt = report.CreatedAt, UpdatedAt = report.UpdatedAt
        };
    }
}
