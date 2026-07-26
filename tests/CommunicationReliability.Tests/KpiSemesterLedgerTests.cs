using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Report.API.Controllers;
using Report.Application.Interfaces;
using Report.Domain.Entities;
using Report.Domain.Enums;
using Report.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class KpiSemesterLedgerTests
{
    [Fact]
    public async Task RuleBelongsToSemester_AndDuplicateNameIsConflict()
    {
        await using var f = new Fixture();
        var semester = f.AddSemester(SemesterStatus.Active);
        var request = new KpiRuleRequest
        {
            SemesterId = semester.Id, Name = "Community impact", Description = "Evidence", MaxPoints = 100
        };

        await f.Controller.CreateRule(request);
        await Assert.ThrowsAsync<ConflictException>(() => f.Controller.CreateRule(request));
        Assert.Equal(semester.Id, (await f.Db.KpiRules.SingleAsync()).SemesterId);
    }

    [Fact]
    public async Task ClosedSemester_CannotReceiveRuleOrAdjustment()
    {
        await using var f = new Fixture();
        var semester = f.AddSemester(SemesterStatus.Closed);
        await Assert.ThrowsAsync<ConflictException>(() => f.Controller.CreateRule(new KpiRuleRequest
        {
            SemesterId = semester.Id, Name = "Closed rule", MaxPoints = 10
        }));
        await Assert.ThrowsAsync<ConflictException>(() => f.Controller.CreateAdjustment(new KpiAdjustmentRequest
        {
            ClubId = Guid.NewGuid(), SemesterId = semester.Id, Points = 5, Reason = "Manual reason"
        }, default));
    }

    [Fact]
    public async Task ManualAdjustment_WritesJwtActorAndSemesterHistory()
    {
        await using var f = new Fixture();
        var semester = f.AddSemester(SemesterStatus.Active);
        var clubId = Guid.NewGuid();
        await f.Controller.CreateAdjustment(new KpiAdjustmentRequest
        {
            ClubId = clubId, SemesterId = semester.Id, Points = 12.5m, Reason = "Verified evidence"
        }, default);

        var row = await f.Db.KpiScoreHistories.SingleAsync();
        Assert.Equal(f.AdminId, row.AdjustedBy);
        Assert.Equal(clubId, row.ClubId);
        Assert.Equal("ManualAdjustment", row.SourceType);
    }

    [Fact]
    public async Task Leaderboard_DoesNotMixSemesters_AndRanksDeterministically()
    {
        await using var f = new Fixture();
        var first = f.AddSemester(SemesterStatus.Active);
        var second = f.AddSemester(SemesterStatus.Draft);
        var clubA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var clubB = Guid.Parse("00000000-0000-0000-0000-000000000002");
        f.Db.KpiScoreHistories.AddRange(
            Score(clubA, first.Id, 10), Score(clubB, first.Id, 20), Score(clubA, second.Id, 999));
        await f.Db.SaveChangesAsync();

        var action = Assert.IsType<OkObjectResult>(await f.Controller.GetLeaderboard(first.Id, default));
        var json = JsonSerializer.Serialize(action.Value);
        Assert.Contains("\"rank\":1", json);
        Assert.Contains(clubB.ToString(), json);
        Assert.DoesNotContain("999", json);
    }

    private static KpiScoreHistory Score(Guid clubId, Guid semesterId, decimal points) => new()
    {
        Id = Guid.NewGuid(), ClubId = clubId, SemesterId = semesterId, Points = points,
        Reason = "Seed", SourceType = "ManualAdjustment", AdjustedBy = Guid.NewGuid()
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid AdminId { get; } = Guid.NewGuid();
        public ReportDbContext Db { get; }
        public KpiController Controller { get; }

        public Fixture()
        {
            Db = new ReportDbContext(new DbContextOptionsBuilder<ReportDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Controller = new KpiController(Db, Mock.Of<ISemesterService>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, AdminId.ToString()),
                            new Claim("role", SystemRoleNames.StudentAffairsAdmin)
                        }, "test"))
                    }
                }
            };
        }

        public Semester AddSemester(SemesterStatus status)
        {
            var semester = new Semester
            {
                Id = Guid.NewGuid(),
                Code = $"S-{Guid.NewGuid():N}"[..12],
                Name = "Semester",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                Status = status
            };
            Db.Semesters.Add(semester);
            Db.SaveChanges();
            return semester;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
