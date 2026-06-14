using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Report.Application.Interfaces;
using Report.Domain.Entities;
using Report.Domain.Enums;

namespace Report.Infrastructure.Persistence
{
    public class ReportRepository : IReportRepository
    {
        private readonly ReportDbContext _context;

        public ReportRepository(ReportDbContext context)
        {
            _context = context;
        }

        public async Task<Domain.Entities.Report?> GetByIdAsync(Guid id)
        {
            return await _context.Reports
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<IEnumerable<Domain.Entities.Report>> GetReportsByClubAsync(Guid clubId, ReportStatus? status, ReportType? type)
        {
            var query = _context.Reports.AsQueryable();

            query = query.Where(r => r.ClubId == clubId);

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(r => r.Type == type.Value);
            }

            return await query.ToListAsync();
        }

        public async Task AddAsync(Domain.Entities.Report report)
        {
            await _context.Reports.AddAsync(report);
        }

        public void Update(Domain.Entities.Report report)
        {
            _context.Reports.Update(report);
        }

        public async Task AddAttachmentAsync(ReportAttachment attachment)
        {
            await _context.ReportAttachments.AddAsync(attachment);
        }
    }
}
