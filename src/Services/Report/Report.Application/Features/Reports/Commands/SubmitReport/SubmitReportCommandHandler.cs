using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Report.Domain.Entities;
using Report.Application.Interfaces;
using Report.Application.Events;
using Shared.Kernel.Exceptions;

namespace Report.Application.Features.Reports.Commands.SubmitReport
{
    public class SubmitReportCommandHandler : IRequestHandler<SubmitReportCommand, bool>
    {
        private readonly IReportRepository _reportRepository;
        private readonly IClubGrpcClient _clubGrpcClient;
        private readonly IEventPublisher _eventPublisher;

        public SubmitReportCommandHandler(
            IReportRepository reportRepository,
            IClubGrpcClient clubGrpcClient,
            IEventPublisher eventPublisher)
        {
            _reportRepository = reportRepository;
            _clubGrpcClient = clubGrpcClient;
            _eventPublisher = eventPublisher;
        }

        public async Task<bool> Handle(SubmitReportCommand request, CancellationToken cancellationToken)
        {
            // 1. Verify club existence via gRPC
            var clubExists = await _clubGrpcClient.CheckClubExistsAsync(request.ClubId);
            if (!clubExists)
            {
                throw new ClubNotFoundException(request.ClubId);
            }

            // 2. Fetch or create report
            var report = await _reportRepository.GetByIdAsync(request.ReportId);
            if (report == null)
            {
                report = new Report.Domain.Entities.Report(
                    request.ReportId,
                    request.ClubId,
                    request.Title,
                    request.Content,
                    request.UserId
                );
                await _reportRepository.AddAsync(report);
            }

            // 3. Execute domain business logic (validates state transitions inside the model)
            report.Submit();

            // 4. Update and persist
            await _reportRepository.UpdateAsync(report);
            await _reportRepository.SaveChangesAsync();

            // 5. Publish Event to Redis Pub/Sub
            var submittedEvent = new ReportSubmittedEvent(
                report.Id,
                report.ClubId,
                report.Title,
                report.CreatedBy,
                report.SubmittedAt ?? DateTime.UtcNow
            );
            await _eventPublisher.PublishAsync("report-events-channel", submittedEvent);

            return true;
        }
    }
}
