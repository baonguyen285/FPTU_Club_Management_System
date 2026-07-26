using System.Threading.Tasks;
using Report.Domain.Entities;

namespace Report.Application.Interfaces
{
    public interface IReportUnitOfWork
    {
        IReportRepository Reports { get; }
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task AddHistoryAsync(ReportRevisionHistory history, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ReportRevisionHistory>> GetHistoryAsync(Guid reportId, CancellationToken cancellationToken = default);
        Task<Semester?> GetSemesterAsync(Guid semesterId, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync();
    }
}
