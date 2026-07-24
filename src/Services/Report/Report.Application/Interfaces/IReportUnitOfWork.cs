using System.Threading.Tasks;
using Report.Domain.Entities;

namespace Report.Application.Interfaces
{
    public interface IReportUnitOfWork
    {
        IReportRepository Reports { get; }
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync();
    }
}
