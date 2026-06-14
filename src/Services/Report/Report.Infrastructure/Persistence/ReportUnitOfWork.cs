using System.Threading.Tasks;
using Report.Application.Interfaces;

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

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
