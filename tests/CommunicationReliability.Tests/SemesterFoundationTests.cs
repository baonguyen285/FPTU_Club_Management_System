using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Report.API.Controllers;
using Report.Application.DTOs;
using Report.Domain.Enums;
using Report.Infrastructure.Persistence;
using Report.Infrastructure.Services;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class SemesterFoundationTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task CreateSemester_ReturnsCanonicalDraft()
    {
        await using var fixture = CreateFixture();

        var result = await fixture.Service.CreateAsync(
            CreateRequest(" fall-2026 "), AdminId, default);

        Assert.Equal("FALL-2026", result.Code);
        Assert.Equal(nameof(SemesterStatus.Draft), result.Status);
        Assert.True(result.StartDate.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task DuplicateCode_ThrowsConflict()
    {
        await using var fixture = CreateFixture();
        await fixture.Service.CreateAsync(CreateRequest("FALL-2026"), AdminId, default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.CreateAsync(CreateRequest(" fall-2026 "), AdminId, default));
    }

    [Fact]
    public async Task InvalidDateRange_ThrowsBadRequest()
    {
        await using var fixture = CreateFixture();
        var request = CreateRequest("SPRING-2027");
        request.EndDate = request.StartDate;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.Service.CreateAsync(request, AdminId, default));
    }

    [Fact]
    public async Task ActivatingNewSemester_ClosesPreviousActiveSemester()
    {
        await using var fixture = CreateFixture();
        var first = await fixture.Service.CreateAsync(CreateRequest("FALL-2026"), AdminId, default);
        var second = await fixture.Service.CreateAsync(CreateRequest("SPRING-2027"), AdminId, default);
        await fixture.Service.ActivateAsync(first.Id, AdminId, default);

        var activated = await fixture.Service.ActivateAsync(second.Id, AdminId, default);

        Assert.Equal(nameof(SemesterStatus.Active), activated.Status);
        var semesters = await fixture.Service.GetAllAsync(default);
        Assert.Single(semesters.Where(x => x.Status == nameof(SemesterStatus.Active)));
        Assert.Equal(nameof(SemesterStatus.Closed), semesters.Single(x => x.Id == first.Id).Status);
    }

    [Fact]
    public async Task ActivateMissingSemester_ThrowsNotFound()
    {
        await using var fixture = CreateFixture();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Service.ActivateAsync(Guid.NewGuid(), AdminId, default));
    }

    [Fact]
    public async Task ClosedSemester_CannotBeActivatedOrUpdated()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(CreateRequest("FALL-2026"), AdminId, default);
        await fixture.Service.ActivateAsync(created.Id, AdminId, default);
        await fixture.Service.CloseAsync(created.Id, AdminId, default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.ActivateAsync(created.Id, AdminId, default));
        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.UpdateAsync(created.Id, UpdateRequest(), AdminId, default));
    }

    [Fact]
    public async Task DraftSemester_CannotBeClosed()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(CreateRequest("FALL-2026"), AdminId, default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.CloseAsync(created.Id, AdminId, default));
    }

    [Fact]
    public async Task UpdateSemester_PreservesCodeAndUpdatesFields()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(CreateRequest("FALL-2026"), AdminId, default);

        var updated = await fixture.Service.UpdateAsync(
            created.Id, UpdateRequest(), AdminId, default);

        Assert.Equal("FALL-2026", updated.Code);
        Assert.Equal("Updated Semester", updated.Name);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task ListAndDetail_ReturnCanonicalDto()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(CreateRequest("FALL-2026"), AdminId, default);

        var detail = await fixture.Service.GetByIdAsync(created.Id, default);
        var list = await fixture.Service.GetAllAsync(default);

        Assert.Equal(created, detail);
        Assert.Single(list);
    }

    [Theory]
    [InlineData(nameof(KpiController.CreateSemester))]
    [InlineData(nameof(KpiController.UpdateSemester))]
    [InlineData(nameof(KpiController.ActivateSemester))]
    [InlineData(nameof(KpiController.CloseSemester))]
    public void SemesterMutationEndpoints_AreAdminOnly(string methodName)
    {
        var method = typeof(KpiController).GetMethod(methodName);
        var authorize = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(SystemRoleNames.StudentAffairsAdmin, authorize.Roles);
    }

    private static CreateSemesterRequest CreateRequest(string code) => new()
    {
        Code = code,
        Name = "Semester",
        StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
    };

    private static UpdateSemesterRequest UpdateRequest() => new()
    {
        Name = "Updated Semester",
        StartDate = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Fixture CreateFixture() => new();

    private sealed class Fixture : IAsyncDisposable
    {
        public ReportDbContext Db { get; }
        public SemesterService Service { get; }

        public Fixture()
        {
            Db = new ReportDbContext(new DbContextOptionsBuilder<ReportDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Service = new SemesterService(Db);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
