using System;
using System.Threading.Tasks;

namespace Report.Application.Interfaces
{
    public interface IClubGrpcClient
    {
        Task<bool> CheckClubExistsAsync(Guid clubId, CancellationToken cancellationToken = default);
        Task<bool> CanSubmitReportsAsync(Guid clubId, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> IsClubManagerAsync(Guid clubId, Guid userId, CancellationToken cancellationToken = default);
    }
}
