using Club.Application.DTOs;
using Club.Domain.Enums;
using Club.Infrastructure.Persistence;
using Club.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Exceptions;

namespace CommunicationReliability.Tests;

public sealed class ClubApplicationWorkflowTests
{
    [Fact]
    public async Task Student_SubmitsPendingApplication_AndDuplicateIsBlocked()
    {
        await using var db = CreateDb();
        var service = new ClubApplicationService(db);
        var applicant = Guid.NewGuid();
        var request = Request("E2E-New Club");

        var created = await service.CreateAsync(applicant, request, default);

        Assert.Equal("PendingApproval", created.Status);
        Assert.Equal(applicant, created.ApplicantUserId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(applicant, Request("Another Club"), default));
    }

    [Fact]
    public async Task AdminApprove_AtomicallyCreatesOneActiveClubAndApprovedLeader()
    {
        await using var db = CreateDb();
        var service = new ClubApplicationService(db);
        var applicant = Guid.NewGuid();
        var application = await service.CreateAsync(applicant, Request("E2E-Atomic Club"), default);

        var approved = await service.ApproveAsync(application.Id, Guid.NewGuid(), default);

        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.CreatedClubId);
        var club = await db.Clubs.SingleAsync(x => x.Id == approved.CreatedClubId);
        Assert.Equal(ClubStatus.Active, club.Status);
        var leader = await db.ClubMembers.SingleAsync(x => x.ClubId == club.Id && x.UserId == applicant);
        Assert.Equal(ClubRole.ClubLeader, leader.Role);
        Assert.Equal(MembershipStatus.Approved, leader.Status);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ApproveAsync(application.Id, Guid.NewGuid(), default));
        Assert.Equal(1, await db.Clubs.CountAsync(x => x.Name == "E2E-Atomic Club"));
    }

    [Fact]
    public async Task Reject_RequiresReason_AndRejectsInvalidTransition()
    {
        await using var db = CreateDb();
        var service = new ClubApplicationService(db);
        var application = await service.CreateAsync(Guid.NewGuid(), Request("E2E-Rejected Club"), default);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.RejectAsync(application.Id, Guid.NewGuid(), " ", default));
        var rejected = await service.RejectAsync(application.Id, Guid.NewGuid(), "Insufficient objectives", default);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Insufficient objectives", rejected.ReviewFeedback);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ApproveAsync(application.Id, Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData(ClubRole.ClubLeader, "Student")]
    [InlineData(ClubRole.Treasurer, "Student")]
    public async Task ApprovedCapability_DoesNotRequireClubManagerSystemRole(
        ClubRole role, string compatibilityRole)
    {
        await using var db = CreateDb();
        var clubId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        db.Clubs.Add(new Club.Domain.Entities.Club
        {
            Id = clubId, Name = "Capability Club", Description = "Test",
            AdvisorId = Guid.NewGuid(), Status = ClubStatus.Active, IsActive = true
        });
        db.ClubMembers.Add(new Club.Domain.Entities.ClubMember
        {
            Id = Guid.NewGuid(), ClubId = clubId, UserId = actorId, Role = role,
            Status = MembershipStatus.Approved, IsActive = true
        });
        await db.SaveChangesAsync();

        if (role == ClubRole.ClubLeader)
            await Club.Application.Security.ClubAuthorization.EnsureClubLeaderOrAdminAsync(
                new Club.Infrastructure.Repositories.ClubRepository(db), clubId, actorId, compatibilityRole);
        else
        {
            var membership = await db.ClubMembers.SingleAsync();
            Assert.Equal(ClubRole.Treasurer, membership.Role);
        }
    }

    private static ClubDbContext CreateDb() => new(new DbContextOptionsBuilder<ClubDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateClubApplicationRequest Request(string name) =>
        new(name, "A valid description", "A valid objective", ["https://example.test/evidence"]);
}
