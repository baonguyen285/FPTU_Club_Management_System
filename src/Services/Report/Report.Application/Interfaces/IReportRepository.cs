using System;
using System.Threading.Tasks;

namespace Report.Application.Interfaces
{
    public interface IReportRepository
    {
        Task<Report.Domain.Entities.Report> GetByIdAsync(Guid id);
        Task AddAsync(Report.Domain.Entities.Report report);
        Task UpdateAsync(Report.Domain.Entities.Report report);
        Task SaveChangesAsync();
    }
}
