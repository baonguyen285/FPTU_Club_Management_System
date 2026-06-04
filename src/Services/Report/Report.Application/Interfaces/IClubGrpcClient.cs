using System;
using System.Threading.Tasks;

namespace Report.Application.Interfaces
{
    public interface IClubGrpcClient
    {
        Task<bool> CheckClubExistsAsync(Guid clubId);
    }
}
