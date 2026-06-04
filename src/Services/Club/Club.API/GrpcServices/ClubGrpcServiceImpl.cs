using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Grpc;
using Club.Infrastructure.Persistence;

namespace Club.API.GrpcServices
{
    public class ClubGrpcServiceImpl : ClubGrpcService.ClubGrpcServiceBase
    {
        private readonly ClubDbContext _context;

        public ClubGrpcServiceImpl(ClubDbContext context)
        {
            _context = context;
        }

        public override async Task<ClubResponse> CheckClubExists(ClubRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.ClubId, out var clubId))
            {
                return new ClubResponse { IsExists = false };
            }

            var exists = await _context.Clubs.AnyAsync(c => c.Id == clubId && c.IsActive);
            return new ClubResponse { IsExists = exists };
        }
    }
}
