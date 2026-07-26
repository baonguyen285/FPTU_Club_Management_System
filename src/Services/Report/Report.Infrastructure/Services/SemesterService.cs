using System.Data;
using Microsoft.EntityFrameworkCore;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Domain.Entities;
using Report.Domain.Enums;
using Report.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;

namespace Report.Infrastructure.Services;

public sealed class SemesterService : ISemesterService
{
    private readonly ReportDbContext _db;

    public SemesterService(ReportDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SemesterDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);
        return semesters.Select(Map).ToList();
    }

    public async Task<SemesterDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var semester = await _db.Semesters.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Semester not found.");
        return Map(semester);
    }

    public async Task<SemesterDto> CreateAsync(
        CreateSemesterRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        ValidateDates(request.StartDate, request.EndDate);
        var code = NormalizeCode(request.Code);
        if (await _db.Semesters.AnyAsync(x => x.Code == code, cancellationToken))
            throw new ConflictException($"Semester code '{code}' already exists.");

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            StartDate = AsUtc(request.StartDate),
            EndDate = AsUtc(request.EndDate),
            Status = SemesterStatus.Draft,
            IsActive = true
        };
        _db.Semesters.Add(semester);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(semester);
    }

    public async Task<SemesterDto> UpdateAsync(
        Guid id,
        UpdateSemesterRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        ValidateDates(request.StartDate, request.EndDate);
        var semester = await FindAsync(id, cancellationToken);
        if (semester.Status == SemesterStatus.Closed)
            throw new ConflictException("Closed semesters cannot be updated.");

        semester.Name = request.Name.Trim();
        semester.StartDate = AsUtc(request.StartDate);
        semester.EndDate = AsUtc(request.EndDate);
        semester.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(semester);
    }

    public async Task<SemesterDto> ActivateAsync(
        Guid id,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var semester = await FindAsync(id, cancellationToken);
        if (semester.Status == SemesterStatus.Closed)
            throw new ConflictException("Closed semesters cannot be activated.");
        if (semester.Status == SemesterStatus.Active)
            return Map(semester);

        var activeSemesters = await _db.Semesters
            .Where(x => x.Status == SemesterStatus.Active && x.Id != id)
            .ToListAsync(cancellationToken);
        foreach (var active in activeSemesters)
        {
            active.Status = SemesterStatus.Closed;
            active.UpdatedAt = DateTime.UtcNow;
        }

        semester.Status = SemesterStatus.Active;
        semester.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return Map(semester);
    }

    public async Task<SemesterDto> CloseAsync(
        Guid id,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var semester = await FindAsync(id, cancellationToken);
        if (semester.Status != SemesterStatus.Active)
            throw new ConflictException("Only an active semester can be closed.");

        semester.Status = SemesterStatus.Closed;
        semester.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(semester);
    }

    private async Task<Semester> FindAsync(Guid id, CancellationToken token) =>
        await _db.Semesters.SingleOrDefaultAsync(x => x.Id == id, token)
        ?? throw new NotFoundException("Semester not found.");

    private static void ValidateDates(DateTime startDate, DateTime endDate)
    {
        if (startDate == default || endDate == default || startDate >= endDate)
            throw new BadRequestException("StartDate must be earlier than EndDate.");
    }

    private static string NormalizeCode(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length is < 2 or > 30)
            throw new BadRequestException("Semester code must contain between 2 and 30 characters.");
        return normalized;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new UnauthorizedException("Invalid actor identity.");
    }

    private static SemesterDto Map(Semester semester) => new(
        semester.Id,
        semester.Code,
        semester.Name,
        semester.StartDate,
        semester.EndDate,
        semester.Status.ToString(),
        semester.CreatedAt,
        semester.UpdatedAt);
}
