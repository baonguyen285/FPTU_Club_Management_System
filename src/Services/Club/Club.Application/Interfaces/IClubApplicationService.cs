using Club.Application.DTOs;

namespace Club.Application.Interfaces;

public interface IClubApplicationService
{
    Task<ClubApplicationDto> CreateAsync(Guid applicantId, CreateClubApplicationRequest request, CancellationToken token);
    Task<IReadOnlyList<ClubApplicationDto>> GetMineAsync(Guid applicantId, CancellationToken token);
    Task<IReadOnlyList<ClubApplicationDto>> GetAllAsync(CancellationToken token);
    Task<ClubApplicationDto> GetAsync(Guid id, Guid actorId, bool isAdmin, CancellationToken token);
    Task<ClubApplicationDto> UpdateAsync(Guid id, Guid applicantId, UpdateClubApplicationRequest request, CancellationToken token);
    Task<ClubApplicationDto> ApproveAsync(Guid id, Guid reviewerId, CancellationToken token);
    Task<ClubApplicationDto> RejectAsync(Guid id, Guid reviewerId, string reason, CancellationToken token);
}
