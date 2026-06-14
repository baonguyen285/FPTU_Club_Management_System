using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Report.Application.Interfaces
{
    public interface IReportRepository
    {
        Task<Domain.Entities.Report?> GetByIdAsync(Guid id);
        Task<IEnumerable<Domain.Entities.Report>> GetReportsByClubAsync(Guid clubId, Domain.Enums.ReportStatus? status, Domain.Enums.ReportType? type);
        Task AddAsync(Domain.Entities.Report report);
        void Update(Domain.Entities.Report report);
        Task AddAttachmentAsync(Domain.Entities.ReportAttachment attachment);
    }
}
