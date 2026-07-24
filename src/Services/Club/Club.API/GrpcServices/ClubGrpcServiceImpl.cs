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

        public override async Task<ClubInfoResponse> GetClubInfo(ClubRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.ClubId, out var clubId))
            {
                return new ClubInfoResponse { Id = string.Empty, Name = string.Empty, IsActive = false };
            }

            var club = await _context.Clubs.FirstOrDefaultAsync(c => c.Id == clubId);
            if (club == null)
            {
                return new ClubInfoResponse { Id = string.Empty, Name = string.Empty, IsActive = false };
            }

            return new ClubInfoResponse
            {
                Id = club.Id.ToString(),
                Name = club.Name,
                IsActive = club.IsActive
            };
        }

        public override async Task<ClubManagerResponse> IsClubManager(ClubManagerRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.ClubId, out var clubId) || !Guid.TryParse(request.UserId, out var userId))
            {
                return new ClubManagerResponse { IsManager = false };
            }

            var isManager = await _context.Set<Club.Domain.Entities.ClubMember>()
                .AnyAsync(m => m.ClubId == clubId && m.UserId == userId 
                            && (m.Role == Club.Domain.Enums.ClubRole.President
                                || m.Role == Club.Domain.Enums.ClubRole.Manager
                                || m.Role == Club.Domain.Enums.ClubRole.Treasurer)
                            && m.Status == Club.Domain.Enums.MembershipStatus.Approved);
                            
            return new ClubManagerResponse { IsManager = isManager };
        }
    }
}
