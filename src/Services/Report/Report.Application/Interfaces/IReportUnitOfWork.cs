using System.Threading.Tasks;

namespace Report.Application.Interfaces
{
    public interface IReportUnitOfWork
    {
        IReportRepository Reports { get; }
        Task<int> SaveChangesAsync();
    }
}
