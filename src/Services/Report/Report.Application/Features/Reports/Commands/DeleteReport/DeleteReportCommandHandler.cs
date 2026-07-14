using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Report.Application.Interfaces;
using Shared.Kernel.Exceptions;

namespace Report.Application.Features.Reports.Commands.DeleteReport
{
    public class DeleteReportCommandHandler : IRequestHandler<DeleteReportCommand>
    {
        private readonly IReportUnitOfWork _unitOfWork;

        public DeleteReportCommandHandler(IReportUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DeleteReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _unitOfWork.Reports.GetByIdAsync(request.Id);
            if (report == null || !report.IsActive)
            {
                throw new NotFoundException("Report not found.");
            }

            report.IsActive = false;
            report.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Reports.Update(report);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
