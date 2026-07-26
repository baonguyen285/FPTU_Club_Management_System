using System.Threading.Tasks;
using Report.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Report.Domain.Entities;

namespace Report.Infrastructure.Persistence
{
    public class ReportUnitOfWork : IReportUnitOfWork
    {
        private readonly ReportDbContext _context;

        public ReportUnitOfWork(ReportDbContext context, IReportRepository reportRepository)
        {
            _context = context;
            Reports = reportRepository;
        }

        public IReportRepository Reports { get; }

        public Task AddOutboxMessageAsync(Report.Domain.Entities.OutboxMessage message, CancellationToken cancellationToken = default)
            => _context.OutboxMessages.AddAsync(message, cancellationToken).AsTask();

        public Task AddHistoryAsync(ReportRevisionHistory history, CancellationToken cancellationToken = default)
            => _context.ReportRevisionHistories.AddAsync(history, cancellationToken).AsTask();

        public async Task<IReadOnlyList<ReportRevisionHistory>> GetHistoryAsync(Guid reportId, CancellationToken cancellationToken = default)
            => await _context.ReportRevisionHistories.AsNoTracking()
                .Where(x => x.ReportId == reportId && x.IsActive)
                .OrderBy(x => x.ChangedAt).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public Task<Semester?> GetSemesterAsync(Guid semesterId, CancellationToken cancellationToken = default)
            => _context.Semesters.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == semesterId && x.IsActive, cancellationToken);

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
