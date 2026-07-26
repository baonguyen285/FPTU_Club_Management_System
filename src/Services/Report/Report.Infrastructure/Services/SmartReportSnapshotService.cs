using Microsoft.EntityFrameworkCore;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;

namespace Report.Infrastructure.Services;

public sealed class SmartReportSnapshotService : ISmartReportSnapshotService
{
    private readonly ReportDbContext _db;
    private readonly IClubReportSnapshotSource _club;
    private readonly IFinanceReportSnapshotSource _finance;

    public SmartReportSnapshotService(
        ReportDbContext db, IClubReportSnapshotSource club, IFinanceReportSnapshotSource finance)
        => (_db, _club, _finance) = (db, club, finance);

    public async Task<ReportGenerationSnapshot> GetPreviewAsync(
        Guid clubId, Guid semesterId, CancellationToken cancellationToken = default)
    {
        if (clubId == Guid.Empty) throw new BadRequestException("clubId is required.");
        if (semesterId == Guid.Empty) throw new BadRequestException("semesterId is required.");

        var semester = await _db.Semesters.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == semesterId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Semester not found.");

        var clubTask = _club.GetAsync(clubId, semesterId, semester.StartDate, semester.EndDate, cancellationToken);
        var financeTask = _finance.GetAsync(clubId, semesterId, semester.StartDate, semester.EndDate, cancellationToken);
        var leaderboardTask = _db.KpiScoreHistories.AsNoTracking()
            .Where(x => x.IsActive && x.SemesterId == semesterId)
            .GroupBy(x => x.ClubId)
            .Select(g => new { ClubId = g.Key, Score = g.Sum(x => x.Points) })
            .OrderByDescending(x => x.Score).ThenBy(x => x.ClubId)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(clubTask, financeTask, leaderboardTask);
        var club = await clubTask;
        var finance = await financeTask;
        var leaderboard = await leaderboardTask;

        var approvedMembers = club.Members.Where(x => x.IsActive && x.Status == 1).ToList();
        var events = club.Events.Select(x => new ReportSnapshotEvent(
            x.Id, x.Title, x.ExpectedDate, EventStatusName(x.Status))).ToList();
        var financeItems = finance.Items.Select(x => new ReportSnapshotFinanceItem(
            x.Id, x.ActivityId, x.Title, x.ProposedDate, x.Status,
            x.RequestedAmount, x.ApprovedAmount, x.ActualAmount)).ToList();
        var ranked = leaderboard.Select((x, index) => new { x.ClubId, x.Score, Rank = index + 1 })
            .SingleOrDefault(x => x.ClubId == clubId);

        var approvedBudget = finance.Items
            .Where(x => x.Status is "Approved" or "PartiallyApproved" or "Settled")
            .Sum(x => x.ApprovedAmount ?? 0m);
        var actualExpense = finance.Items
            .Where(x => x.Status == "Settled" && x.ActualAmount.HasValue)
            .Sum(x => x.ActualAmount!.Value);

        var sources = new List<SourceReference>
        {
            new("Club", clubId.ToString(), club.ClubName, $"/gateway/clubs/{clubId}"),
            new("Semester", semesterId.ToString(), semester.Code, $"/gateway/kpi/semesters/{semesterId}")
        };
        sources.AddRange(events.Select(x => new SourceReference(
            "Event", x.Id.ToString(), x.Title, $"/gateway/events/{x.Id}")));
        sources.AddRange(financeItems.Select(x => new SourceReference(
            "Finance", x.Id.ToString(), x.Title, $"/gateway/finance/proposals/{x.Id}")));

        return new ReportGenerationSnapshot(
            clubId, club.ClubName, semesterId, semester.Code,
            approvedMembers.Count,
            approvedMembers.Count(x => x.JoinedAt >= semester.StartDate && x.JoinedAt <= semester.EndDate),
            club.Events.Count(x => x.Status == 4),
            club.Events.Count(x => x.Status == 5),
            finance.Items.Count == 0 ? null : approvedBudget,
            finance.Items.Any(x => x.Status == "Settled" && x.ActualAmount.HasValue) ? actualExpense : null,
            finance.RemainingBalance,
            ranked?.Score,
            ranked?.Rank,
            events,
            financeItems,
            sources,
            new ReportSnapshotAvailability(true, true, true, true, finance.RemainingBalance.HasValue, true));
    }

    private static string EventStatusName(int status) => status switch
    {
        0 => "Draft", 1 => "PendingApproval", 2 => "Approved", 3 => "Rejected",
        4 => "Completed", 5 => "Cancelled", _ => "Unknown"
    };
}
