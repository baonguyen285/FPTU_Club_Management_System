using Report.Application.DTOs;

namespace Report.Application.Interfaces;

public interface ISemesterService
{
    Task<IReadOnlyList<SemesterDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<SemesterDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SemesterDto> CreateAsync(CreateSemesterRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<SemesterDto> UpdateAsync(Guid id, UpdateSemesterRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<SemesterDto> ActivateAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
    Task<SemesterDto> CloseAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
}
