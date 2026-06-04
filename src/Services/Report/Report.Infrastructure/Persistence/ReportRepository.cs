using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Report.Application.Interfaces;
using Report.Domain.Entities;
using Report.Infrastructure.Persistence;

namespace Report.Infrastructure.Persistence
{
    public class ReportRepository : IReportRepository
    {
        private readonly ReportDbContext _context;

        public ReportRepository(ReportDbContext context)
        {
            _context = context;
        }

        public async Task<Report.Domain.Entities.Report> GetByIdAsync(Guid id)
        {
            return await _context.Reports.FindAsync(id);
        }

        public async Task AddAsync(Report.Domain.Entities.Report report)
        {
            await _context.Reports.AddAsync(report);
        }

        public async Task UpdateAsync(Report.Domain.Entities.Report report)
        {
            // EF Core change tracker handles it, but marking state is safe
            _context.Entry(report).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
