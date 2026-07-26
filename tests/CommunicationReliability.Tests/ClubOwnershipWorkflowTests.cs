using System.Reflection;
using AutoMapper;
using Club.Application.DTOs;
using Club.Application.Features.Clubs.Commands.ReviewClub;
using Club.Application.Features.Events.Commands.ChangeEventStatus;
using Club.Application.Features.Events.Commands.CreateEvent;
using Club.Application.Features.Members.Commands.ApproveMember;
using Club.Application.Features.Members.Commands.JoinClub;
using Club.Application.Interfaces;
using Club.Domain.Entities;
using Club.Domain.Enums;
using Club.Infrastructure.Persistence;
using Club.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;
using ClubPermission = Shared.Kernel.Grpc.ClubAccess.V1.ClubPermission;

namespace CommunicationReliability.Tests;

public sealed class ClubOwnershipWorkflowTests
{
    [Fact]
    public async Task Student_CanCreateOnePendingJoinRequest()
    {
        await using var fixture = CreateFixture();
        var club = fixture.AddClub();
        var studentId = Guid.NewGuid();
        await fixture.SaveAsync();
        var handler = new JoinClubCommandHandler(fixture.UnitOfWork, fixture.Mapper);

        var result = await handler.Handle(
            new JoinClubCommand { ClubId = club.Id, UserId = studentId }, default);

        Assert.Equal(MembershipStatus.Pending, result.Status);
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new JoinClubCommand { ClubId = club.Id, UserId = studentId }, default));
    }

    [Fact]
    public async Task ClubLeader_CanApprovePendingMemberInOwnClub()
    {
        await using var fixture = CreateFixture();
        var club = fixture.AddClub();
        var leader = fixture.AddMember(club.Id, ClubRole.ClubLeader, MembershipStatus.Approved);
        var pending = fixture.AddMember(club.Id, ClubRole.Member, MembershipStatus.Pending);
        await fixture.SaveAsync();

        var result = await new ApproveMemberCommandHandler(fixture.UnitOfWork, fixture.Mapper).Handle(
            new ApproveMemberCommand
            {
                ClubId = club.Id, UserId = pending.UserId, ActorId = leader.UserId,
                ActorRole = SystemRoleNames.ClubManager
            }, default);

        Assert.Equal(MembershipStatus.Approved, result.Status);
    }

    [Theory]
    [InlineData(ClubRole.Member)]
    [InlineData(ClubRole.Treasurer)]
    public async Task NonLeader_CannotApproveMembership(ClubRole actorClubRole)
    {
        await using var fixture = CreateFixture();
        var club = fixture.AddClub();
        var actor = fixture.AddMember(club.Id, actorClubRole, MembershipStatus.Approved);
        var pending = fixture.AddMember(club.Id, ClubRole.Member, MembershipStatus.Pending);
        await fixture.SaveAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ApproveMemberCommandHandler(fixture.UnitOfWork, fixture.Mapper).Handle(
                new ApproveMemberCommand
                {
                    ClubId = club.Id, UserId = pending.UserId, ActorId = actor.UserId,
                    ActorRole = SystemRoleNames.ClubManager
                }, default));
    }

    [Fact]
    public async Task ClubLeader_CannotApproveMembershipInAnotherClub()
    {
        await using var fixture = CreateFixture();
        var ownedClub = fixture.AddClub();
        var otherClub = fixture.AddClub();
        var leader = fixture.AddMember(ownedClub.Id, ClubRole.ClubLeader, MembershipStatus.Approved);
        var pending = fixture.AddMember(otherClub.Id, ClubRole.Member, MembershipStatus.Pending);
        await fixture.SaveAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ApproveMemberCommandHandler(fixture.UnitOfWork, fixture.Mapper).Handle(
                new ApproveMemberCommand
                {
                    ClubId = otherClub.Id, UserId = pending.UserId, ActorId = leader.UserId,
                    ActorRole = SystemRoleNames.ClubManager
                }, default));
    }

    [Fact]
    public async Task StudentAffairsAdmin_CanApproveWithoutClubMembership()
    {
        await using var fixture = CreateFixture();
        var club = fixture.AddClub();
        var pending = fixture.AddMember(club.Id, ClubRole.Member, MembershipStatus.Pending);
        await fixture.SaveAsync();

        var result = await new ApproveMemberCommandHandler(fixture.UnitOfWork, fixture.Mapper).Handle(
            new ApproveMemberCommand
            {
                ClubId = club.Id, UserId = pending.UserId, ActorId = Guid.NewGuid(),
                ActorRole = SystemRoleNames.StudentAffairsAdmin
            }, default);

        Assert.Equal(MembershipStatus.Approved, result.Status);
    }

    [Fact]
    public async Task ApprovedMembership_CannotBeApprovedAgain()
    {
        await using var fixture = CreateFixture();
        var club = fixture.AddClub();
        var member = fixture.AddMember(club.Id, ClubRole.Member, MembershipStatus.Approved);
        await fixture.SaveAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            new ApproveMemberCommandHandler(fixture.UnitOfWork, fixture.Mapper).Handle(
                new ApproveMemberCommand
                {
                    ClubId = club.Id, UserId = member.UserId, ActorId = Guid.NewGuid(),
                    ActorRole = SystemRoleNames.StudentAffairsAdmin
                }, default));
    }

    [Fact]
    public async Task ClubLeader_CanCreateActivityOnlyForOwnClub()
    {
        await using var fixture = CreateFixture();
        var ownClub = fixture.AddClub();
        var otherClub = fixture.AddClub();
        var leader = fixture.AddMember(ownClub.Id, ClubRole.ClubLeader, MembershipStatus.Approved);
        await fixture.SaveAsync();
        var handler = new CreateEventCommandHandler(
            fixture.UnitOfWork, fixture.Mapper, fixture.EventPublisher.Object);

        var result = await handler.Handle(NewEvent(ownClub.Id, leader.UserId), default);
        Assert.Equal(EventStatus.Draft, result.Status);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(NewEvent(otherClub.Id, leader.UserId), default));
    }

    [Fact]
    public async Task Member_CannotCreateActivity()
    {
        await using var fixture = CreateFixture();
        var club = fixture.AddClub();
        var member = fixture.AddMember(club.Id, ClubRole.Member, MembershipStatus.Approved);
        await fixture.SaveAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new CreateEventCommandHandler(
                fixture.UnitOfWork, fixture.Mapper, fixture.EventPublisher.Object)
                .Handle(NewEvent(club.Id, member.UserId), default));
    }

    [Fact]
    public async Task Activity_StatusWorkflow_RejectsInvalidTransition()
    {
        await using var fixture = CreateFixture();
        var club = fixture.AddClub();
        var leader = fixture.AddMember(club.Id, ClubRole.ClubLeader, MembershipStatus.Approved);
        var activity = fixture.AddEvent(club.Id, EventStatus.Draft);
        await fixture.SaveAsync();
        var handler = new ChangeEventStatusCommandHandler(fixture.UnitOfWork, fixture.Mapper);

        var submitted = await handler.Handle(new ChangeEventStatusCommand
        {
            Id = activity.Id, TargetStatus = EventStatus.PendingApproval,
            ActorId = leader.UserId, ActorRole = SystemRoleNames.ClubManager
        }, default);
        Assert.Equal(EventStatus.PendingApproval, submitted.Status);

        var approved = await handler.Handle(new ChangeEventStatusCommand
        {
            Id = activity.Id, TargetStatus = EventStatus.Approved,
            ActorId = Guid.NewGuid(), ActorRole = SystemRoleNames.StudentAffairsAdmin
        }, default);
        Assert.Equal(EventStatus.Approved, approved.Status);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new ChangeEventStatusCommand
        {
            Id = activity.Id, TargetStatus = EventStatus.Rejected,
            ActorId = Guid.NewGuid(), ActorRole = SystemRoleNames.StudentAffairsAdmin
        }, default));
    }

    [Fact]
    public async Task Club_InvalidStatusTransition_ThrowsConflict()
    {
        await using var fixture = CreateFixture();
        var club = fixture.AddClub(ClubStatus.Inactive);
        await fixture.SaveAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            new ReviewClubCommandHandler(fixture.UnitOfWork, fixture.Mapper).Handle(
                new ReviewClubCommand { Id = club.Id, Status = ClubStatus.Active }, default));
    }

    [Theory]
    [InlineData(ClubRole.ClubLeader, ClubPermission.ManageMembers, true)]
    [InlineData(ClubRole.ClubLeader, ClubPermission.ManageActivities, true)]
    [InlineData(ClubRole.Treasurer, ClubPermission.ManageFinance, true)]
    [InlineData(ClubRole.ClubLeader, ClubPermission.ManageFinance, false)]
    [InlineData(ClubRole.Member, ClubPermission.ManageMembers, false)]
    [InlineData(ClubRole.LegacyManager, ClubPermission.SubmitReports, false)]
    public void GrpcPermissionProjection_IsCanonical(
        ClubRole role, ClubPermission permission, bool expected)
    {
        var method = typeof(Club.API.GrpcServices.ClubAccessGrpcServiceImpl)
            .GetMethod("IsAllowed", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(expected, method.Invoke(null, [role, permission]));
    }

    private static CreateEventCommand NewEvent(Guid clubId, Guid actorId) => new()
    {
        ClubId = clubId,
        ActorId = actorId,
        ActorRole = SystemRoleNames.ClubManager,
        Title = "Valid activity",
        Description = "Activity description",
        ExpectedDate = DateTime.UtcNow.AddDays(7),
        Location = "Campus"
    };

    private static Fixture CreateFixture() => new();

    private sealed class Fixture : IAsyncDisposable
    {
        public ClubDbContext Db { get; }
        public UnitOfWork UnitOfWork { get; }
        public IMapper Mapper { get; }
        public Mock<IClubEventPublisher> EventPublisher { get; } = new();

        public Fixture()
        {
            Db = new ClubDbContext(new DbContextOptionsBuilder<ClubDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            UnitOfWork = new UnitOfWork(Db);
            Mapper = new MapperConfiguration(
                cfg => cfg.AddProfile<MappingProfile>(),
                NullLoggerFactory.Instance).CreateMapper();
            EventPublisher
                .Setup(publisher => publisher.PublishActivityCreatedAsync(
                    It.IsAny<Club.Domain.Entities.Event>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public Club.Domain.Entities.Club AddClub(ClubStatus status = ClubStatus.Active)
        {
            var club = new Club.Domain.Entities.Club
            {
                Id = Guid.NewGuid(), Name = "Test Club", Description = "Description",
                AdvisorId = Guid.NewGuid(), Status = status, IsActive = true
            };
            Db.Clubs.Add(club);
            return club;
        }

        public ClubMember AddMember(Guid clubId, ClubRole role, MembershipStatus status)
        {
            var member = new ClubMember
            {
                Id = Guid.NewGuid(), ClubId = clubId, UserId = Guid.NewGuid(),
                Role = role, Status = status, IsActive = true
            };
            Db.ClubMembers.Add(member);
            return member;
        }

        public Club.Domain.Entities.Event AddEvent(Guid clubId, EventStatus status)
        {
            var clubEvent = new Club.Domain.Entities.Event
            {
                Id = Guid.NewGuid(), ClubId = clubId, Title = "Activity",
                Description = "Description", Location = "Campus",
                ExpectedDate = DateTime.UtcNow.AddDays(5), Status = status, IsActive = true
            };
            Db.Events.Add(clubEvent);
            return clubEvent;
        }

        public Task SaveAsync() => Db.SaveChangesAsync();
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
