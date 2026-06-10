using System.Threading;
using System.Threading.Tasks;

namespace Club.Application.Interfaces
{
    public interface IUnitOfWork
    {
        IClubRepository Clubs { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
