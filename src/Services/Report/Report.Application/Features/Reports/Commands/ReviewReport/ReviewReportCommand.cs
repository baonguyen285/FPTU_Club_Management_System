using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Report.Application.Features.Reports.Commands.ReviewReport
{
    public class ReviewReportCommand : IRequest<ReportDto>
    {
        public Guid ReportId { get; set; }
        public Guid UserId { get; set; }
        public bool IsApproved { get; set; }
        public string? ReviewNote { get; set; }
    }

    public class ReviewReportCommandHandler : IRequestHandler<ReviewReportCommand, ReportDto>
    {
        private readonly IReportUnitOfWork _uow;
        private readonly IClubGrpcClient _grpcClient;

        public ReviewReportCommandHandler(IReportUnitOfWork uow, IClubGrpcClient grpcClient)
        {
            _uow = uow;
            _grpcClient = grpcClient;
        }

        public async Task<ReportDto> Handle(ReviewReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _uow.Reports.GetByIdAsync(request.ReportId);

            if (report == null)
            {
                throw new NotFoundException($"Report with ID {request.ReportId} not found.");
            }

            // Check if user is manager/president of the club
            bool isManager = await _grpcClient.IsClubManagerAsync(report.ClubId, request.UserId);
            if (!isManager)
            {
                throw new UnauthorizedException("You do not have permission to review reports for this club.");
            }

            if (request.IsApproved)
            {
                report.Approve(request.UserId, request.ReviewNote);
            }
            else
            {
                report.Reject(request.UserId, request.ReviewNote ?? "No reason provided.");
            }

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
                ReviewedBy = report.ReviewedBy,
                ReviewNote = report.ReviewNote,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt
            };
        }
    }
}
