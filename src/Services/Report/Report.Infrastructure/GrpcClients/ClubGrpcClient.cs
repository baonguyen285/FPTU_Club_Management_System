using System;
using System.Threading.Tasks;
using Shared.Kernel.Grpc;
using Report.Application.Interfaces;

namespace Report.Infrastructure.GrpcClients
{
    public class ClubGrpcClient : IClubGrpcClient
    {
        private readonly ClubGrpcService.ClubGrpcServiceClient _client;

        public ClubGrpcClient(ClubGrpcService.ClubGrpcServiceClient client)
        {
            _client = client;
        }

        public async Task<bool> CheckClubExistsAsync(Guid clubId)
        {
            try
            {
                var request = new ClubRequest { ClubId = clubId.ToString() };
                var response = await _client.CheckClubExistsAsync(request);
                return response.IsExists;
            }
            catch
            {
                // Fallback to false on communication errors
                return false;
            }
        }

        public async Task<bool> IsClubManagerAsync(Guid clubId, Guid userId)
        {
            try
            {
                var request = new ClubManagerRequest 
                { 
                    ClubId = clubId.ToString(),
                    UserId = userId.ToString()
                };
                var response = await _client.IsClubManagerAsync(request);
                return response.IsManager;
            }
            catch
            {
                return false;
            }
        }
    }
}
