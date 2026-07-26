using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Report.API.Controllers;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Domain.Entities;
using Report.Domain.Enums;
using Report.Infrastructure.Persistence;
using Report.Infrastructure.Services;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class SmartReportSnapshotTests
{
    private static readonly Guid ClubId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherClubId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SemesterId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid LeaderId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task Preview_UsesRealSourceValues_AndPreservesUnavailableMetricsAsNull()
    {
        await using var db = CreateDb();
        db.Semesters.Add(Semester());
        db.KpiScoreHistories.AddRange(
            Kpi(ClubId, 75), Kpi(OtherClubId, 90));
        await db.SaveChangesAsync();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var club = new StubClubSource(new ClubSnapshotSourceData("Robotics Club",
            [
                new(Guid.NewGuid(), LeaderId, 2, 1, start.AddDays(-10), true),
                new(Guid.NewGuid(), Guid.NewGuid(), 1, 1, start.AddDays(5), true),
                new(Guid.NewGuid(), Guid.NewGuid(), 1, 0, start.AddDays(6), true)
            ],
            [
                new(Guid.NewGuid(), "Demo Day", start.AddDays(10), 4, true),
                new(Guid.NewGuid(), "Cancelled Workshop", start.AddDays(20), 5, false)
            ]));
        var finance = new StubFinanceSource(new FinanceSnapshotSourceData(
            [
                new(Guid.NewGuid(), null, "Demo budget", start.AddDays(3), "Approved", 1000, 900, null),
                new(Guid.NewGuid(), null, "Workshop budget", start.AddDays(4), "Settled", 500, 400, 350)
            ], null));

        var snapshot = await new SmartReportSnapshotService(db, club, finance)
            .GetPreviewAsync(ClubId, SemesterId);

        Assert.Equal(2, snapshot.TotalMembers);
        Assert.Equal(1, snapshot.NewMembers);
        Assert.Equal(1, snapshot.CompletedEvents);
        Assert.Equal(1, snapshot.CancelledEvents);
        Assert.Equal(1300, snapshot.ApprovedBudget);
        Assert.Equal(350, snapshot.ActualExpense);
        Assert.Null(snapshot.RemainingBalance);
        Assert.Equal(75, snapshot.KpiScore);
        Assert.Equal(2, snapshot.KpiRank);
        Assert.False(snapshot.Availability.Balance);
        Assert.Equal(2, snapshot.Events.Count);
        Assert.Equal(2, snapshot.FinanceItems.Count);
        Assert.Contains(snapshot.Sources, x => x.Type == "Event");
        Assert.Equal(1, club.CallCount);
        Assert.Equal(1, finance.CallCount);
    }

    [Fact]
    public async Task Preview_MissingSemester_ThrowsNotFound_WithoutCallingDependencies()
    {
        await using var db = CreateDb();
        var club = new StubClubSource(null!);
        var finance = new StubFinanceSource(null!);
        var service = new SmartReportSnapshotService(db, club, finance);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetPreviewAsync(ClubId, SemesterId));
        Assert.Equal(0, club.CallCount);
        Assert.Equal(0, finance.CallCount);
    }

    [Fact]
    public async Task Endpoint_ApprovedLeaderForExactClub_Returns200()
    {
        var controller = Controller(SystemRoleNames.Student, true);
        var result = await controller.PreviewSmartAssistant(ClubId, SemesterId, default);
        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Treasurer")]
    public async Task Endpoint_NonLeaderCapability_Returns403(string role)
    {
        var controller = Controller(role, false);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => controller.PreviewSmartAssistant(ClubId, SemesterId, default));
    }

    [Fact]
    public async Task Endpoint_AdminReadPolicy_Returns200_WithoutMembershipCheck()
    {
        var club = new Mock<IClubGrpcClient>(MockBehavior.Strict);
        var controller = Controller(SystemRoleNames.StudentAffairsAdmin, false, club);
        var result = await controller.PreviewSmartAssistant(ClubId, SemesterId, default);
        Assert.IsType<OkObjectResult>(result);
        club.VerifyNoOtherCalls();
    }

    private static ReportsController Controller(
        string role, bool allowed, Mock<IClubGrpcClient>? clubMock = null)
    {
        var snapshot = Snapshot();
        var smart = new Mock<ISmartReportSnapshotService>();
        smart.Setup(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var club = clubMock ?? new Mock<IClubGrpcClient>();
        if (clubMock is null)
            club.Setup(x => x.CanSubmitReportsAsync(ClubId, LeaderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allowed);
        var controller = new ReportsController(
            Mock.Of<IMediator>(), club.Object, smart.Object, Mock.Of<IReportValidationService>(),
            Mock.Of<IReportDraftGenerationService>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(JwtRegisteredClaimNames.Sub, LeaderId.ToString()), new Claim("role", role)],
                    "test", JwtRegisteredClaimNames.Name, "role"))
            }
        };
        return controller;
    }

    private static ReportGenerationSnapshot Snapshot() => new(
        ClubId, "Club", SemesterId, "SPRING-2026", 1, 0, 0, 0,
        null, null, null, null, null, [], [], [],
        new(true, true, true, true, false, true));

    private static ReportDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ReportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Semester Semester() => new()
    {
        Id = SemesterId, Code = "SPRING-2026", Name = "Spring 2026",
        StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc),
        Status = SemesterStatus.Active, IsActive = true
    };

    private static KpiScoreHistory Kpi(Guid clubId, decimal points) => new()
    {
        Id = Guid.NewGuid(), ClubId = clubId, SemesterId = SemesterId,
        Points = points, Reason = "Authoritative score", SourceType = "Test",
        AdjustedBy = LeaderId, IsActive = true
    };

    private sealed class StubClubSource(ClubSnapshotSourceData data) : IClubReportSnapshotSource
    {
        public int CallCount { get; private set; }
        public Task<ClubSnapshotSourceData> GetAsync(
            Guid clubId, Guid semesterId, DateTime start, DateTime end,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(data);
        }
    }

    private sealed class StubFinanceSource(FinanceSnapshotSourceData data) : IFinanceReportSnapshotSource
    {
        public int CallCount { get; private set; }
        public Task<FinanceSnapshotSourceData> GetAsync(
            Guid clubId, Guid semesterId, DateTime start, DateTime end,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(data);
        }
    }
}
