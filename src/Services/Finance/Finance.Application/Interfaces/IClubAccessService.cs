namespace Finance.Application.Interfaces;

public interface IClubAccessService
{
    Task<bool> ClubExistsAsync(Guid clubId, CancellationToken cancellationToken = default);
    Task<bool> CanManageFinanceAsync(Guid clubId, Guid userId, CancellationToken cancellationToken = default);
}
