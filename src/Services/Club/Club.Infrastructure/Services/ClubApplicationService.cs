using System.Text.Json;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Club.Domain.Entities;
using Club.Domain.Enums;
using Club.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Exceptions;

namespace Club.Infrastructure.Services;

public sealed class ClubApplicationService : IClubApplicationService
{
    private readonly ClubDbContext _db;
    private readonly IClubEventPublisher? _events;

    public ClubApplicationService(ClubDbContext db, IClubEventPublisher? events = null)
        => (_db, _events) = (db, events);

    public async Task<ClubApplicationDto> CreateAsync(Guid applicantId, CreateClubApplicationRequest request, CancellationToken token)
    {
        Validate(request.ProposedClubName, request.Description, request.Objectives);
        var name = request.ProposedClubName.Trim();
        if (await _db.ClubApplications.AnyAsync(x =>
                x.ApplicantUserId == applicantId &&
                x.Status == ClubApplicationStatus.PendingApproval, token))
            throw new ConflictException("Applicant already has a pending club application.");
        if (await _db.Clubs.AnyAsync(x => x.Name == name && x.IsActive, token) ||
            await _db.ClubApplications.AnyAsync(x => x.ProposedClubName == name &&
                x.Status == ClubApplicationStatus.PendingApproval, token))
            throw new ConflictException("An active club or pending application already uses this name.");

        var entity = new ClubApplication
        {
            Id = Guid.NewGuid(),
            ApplicantUserId = applicantId,
            ProposedClubName = name,
            Description = request.Description.Trim(),
            Objectives = request.Objectives.Trim(),
            EvidenceUrlsJson = SerializeUrls(request.EvidenceUrls),
            Status = ClubApplicationStatus.PendingApproval,
            SubmittedAt = DateTime.UtcNow,
            IsActive = true
        };
        _db.ClubApplications.Add(entity);
        await _db.SaveChangesAsync(token);
        return Map(entity);
    }

    public async Task<IReadOnlyList<ClubApplicationDto>> GetMineAsync(Guid applicantId, CancellationToken token) =>
        (await _db.ClubApplications.AsNoTracking().Where(x => x.ApplicantUserId == applicantId)
            .OrderByDescending(x => x.SubmittedAt).ToListAsync(token)).Select(Map).ToList();

    public async Task<IReadOnlyList<ClubApplicationDto>> GetAllAsync(CancellationToken token) =>
        (await _db.ClubApplications.AsNoTracking().OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(token)).Select(Map).ToList();

    public async Task<ClubApplicationDto> GetAsync(Guid id, Guid actorId, bool isAdmin, CancellationToken token)
    {
        var entity = await FindAsync(id, token);
        if (!isAdmin && entity.ApplicantUserId != actorId)
            throw new ForbiddenException("Application belongs to another user.");
        return Map(entity);
    }

    public async Task<ClubApplicationDto> UpdateAsync(Guid id, Guid applicantId, UpdateClubApplicationRequest request, CancellationToken token)
    {
        Validate(request.ProposedClubName, request.Description, request.Objectives);
        var entity = await FindAsync(id, token);
        if (entity.ApplicantUserId != applicantId)
            throw new ForbiddenException("Application belongs to another user.");
        if (entity.Status != ClubApplicationStatus.PendingApproval)
            throw new ConflictException("Only a pending application can be edited.");
        var name = request.ProposedClubName.Trim();
        if (await _db.ClubApplications.AnyAsync(x => x.Id != id && x.ProposedClubName == name &&
                x.Status == ClubApplicationStatus.PendingApproval, token) ||
            await _db.Clubs.AnyAsync(x => x.Name == name && x.IsActive, token))
            throw new ConflictException("An active club or pending application already uses this name.");
        entity.ProposedClubName = name;
        entity.Description = request.Description.Trim();
        entity.Objectives = request.Objectives.Trim();
        entity.EvidenceUrlsJson = SerializeUrls(request.EvidenceUrls);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(token);
        return Map(entity);
    }

    public async Task<ClubApplicationDto> ApproveAsync(Guid id, Guid reviewerId, CancellationToken token)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(token)
            : null;
        var entity = await _db.ClubApplications.SingleOrDefaultAsync(x => x.Id == id, token)
            ?? throw new NotFoundException("Club application not found.");
        if (entity.Status != ClubApplicationStatus.PendingApproval)
            throw new ConflictException("Only a pending application can be approved.");
        if (entity.CreatedClubId.HasValue)
            throw new ConflictException("Application already created a club.");
        if (await _db.Clubs.AnyAsync(x => x.Name == entity.ProposedClubName && x.IsActive, token))
            throw new ConflictException("An active club already uses this name.");

        var club = new Domain.Entities.Club
        {
            Id = Guid.NewGuid(),
            Name = entity.ProposedClubName,
            Description = entity.Description,
            AdvisorId = reviewerId,
            Status = ClubStatus.Active,
            IsActive = true
        };
        var leader = new ClubMember
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            UserId = entity.ApplicantUserId,
            Role = ClubRole.ClubLeader,
            Status = MembershipStatus.Approved,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        _db.Clubs.Add(club);
        _db.ClubMembers.Add(leader);
        entity.Status = ClubApplicationStatus.Approved;
        entity.ReviewedAt = DateTime.UtcNow;
        entity.ReviewedByUserId = reviewerId;
        entity.CreatedClubId = club.Id;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(token);
        if (transaction is not null)
            await transaction.CommitAsync(token);
        if (_events is not null)
            await _events.PublishClubApplicationReviewedAsync(entity, token);
        return Map(entity);
    }

    public async Task<ClubApplicationDto> RejectAsync(Guid id, Guid reviewerId, string reason, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BadRequestException("Reject reason is required.");
        var entity = await FindAsync(id, token);
        if (entity.Status != ClubApplicationStatus.PendingApproval)
            throw new ConflictException("Only a pending application can be rejected.");
        entity.Status = ClubApplicationStatus.Rejected;
        entity.ReviewFeedback = reason.Trim();
        entity.ReviewedAt = DateTime.UtcNow;
        entity.ReviewedByUserId = reviewerId;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(token);
        if (_events is not null)
            await _events.PublishClubApplicationReviewedAsync(entity, token);
        return Map(entity);
    }

    private async Task<ClubApplication> FindAsync(Guid id, CancellationToken token) =>
        await _db.ClubApplications.SingleOrDefaultAsync(x => x.Id == id, token)
        ?? throw new NotFoundException("Club application not found.");

    private static void Validate(string name, string description, string objectives)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new BadRequestException("Proposed club name is required and cannot exceed 150 characters.");
        if (string.IsNullOrWhiteSpace(description))
            throw new BadRequestException("Description is required.");
        if (string.IsNullOrWhiteSpace(objectives))
            throw new BadRequestException("Objectives are required.");
    }

    private static string? SerializeUrls(IReadOnlyList<string>? urls)
    {
        var clean = urls?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
        return clean is { Count: > 0 } ? JsonSerializer.Serialize(clean) : null;
    }

    private static ClubApplicationDto Map(ClubApplication x) => new(
        x.Id, x.ApplicantUserId, x.ProposedClubName, x.Description, x.Objectives,
        string.IsNullOrWhiteSpace(x.EvidenceUrlsJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<List<string>>(x.EvidenceUrlsJson) ?? [],
        x.Status.ToString(), x.ReviewFeedback, x.SubmittedAt, x.ReviewedAt,
        x.ReviewedByUserId, x.CreatedClubId);
}
