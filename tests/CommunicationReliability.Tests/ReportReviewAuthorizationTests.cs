using Moq;
using Report.Application.Features.Reports.Commands.ReviewReport;
using Report.Application.Interfaces;
using Report.Domain.Enums;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class ReportReviewAuthorizationTests
{
    private readonly Mock<IReportRepository> _reports = new();
    private readonly Mock<IReportUnitOfWork> _uow = new();
    private readonly ReportReviewAuthorizationTestsFixture _fixture = new();

    public ReportReviewAuthorizationTests()
    {
        _uow.SetupGet(x => x.Reports).Returns(_reports.Object);
        _uow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
    }

    [Fact]
    public async Task StudentAffairsAdmin_CanReviewPendingReport()
    {
        var report = _fixture.CreatePendingReport();
        _reports.Setup(x => x.GetByIdAsync(report.Id)).ReturnsAsync(report);

        var result = await new ReviewReportCommandHandler(_uow.Object).Handle(
            _fixture.Command(report.Id, SystemRoleNames.StudentAffairsAdmin, approved: true),
            CancellationToken.None);

        Assert.Equal(nameof(ReportStatus.Approved), result.Status);
        _uow.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Theory]
    [InlineData(SystemRoleNames.ClubManager)]
    [InlineData(SystemRoleNames.Student)]
    public async Task NonAdmin_CannotReview(string role)
    {
        var report = _fixture.CreatePendingReport();
        _reports.Setup(x => x.GetByIdAsync(report.Id)).ReturnsAsync(report);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ReviewReportCommandHandler(_uow.Object).Handle(
                _fixture.Command(report.Id, role, approved: true), CancellationToken.None));
    }

    [Fact]
    public void ReviewRequest_DoesNotAcceptActorIdentityOrRole()
    {
        var properties = typeof(Report.API.Controllers.ReviewReportRequest).GetProperties()
            .Select(x => x.Name).ToArray();
        Assert.DoesNotContain("UserId", properties);
        Assert.DoesNotContain("ActorRole", properties);
    }

    [Fact]
    public async Task InvalidTransition_ReturnsConflict()
    {
        var report = _fixture.CreatePendingReport();
        report.Approve(Guid.NewGuid());
        _reports.Setup(x => x.GetByIdAsync(report.Id)).ReturnsAsync(report);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new ReviewReportCommandHandler(_uow.Object).Handle(
                _fixture.Command(report.Id, SystemRoleNames.StudentAffairsAdmin, approved: true),
                CancellationToken.None));
    }

    [Fact]
    public async Task RejectWithoutFeedback_IsRejected()
    {
        var report = _fixture.CreatePendingReport();
        _reports.Setup(x => x.GetByIdAsync(report.Id)).ReturnsAsync(report);
        var command = _fixture.Command(report.Id, SystemRoleNames.StudentAffairsAdmin, approved: false);
        command.ReviewNote = " ";

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new ReviewReportCommandHandler(_uow.Object).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task MissingReport_ReturnsNotFound()
    {
        _reports.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Report.Domain.Entities.Report?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ReviewReportCommandHandler(_uow.Object).Handle(
                _fixture.Command(Guid.NewGuid(), SystemRoleNames.StudentAffairsAdmin, approved: true),
                CancellationToken.None));
    }
}

internal sealed class ReportReviewAuthorizationTestsFixture
{
    public Report.Domain.Entities.Report CreatePendingReport() =>
        new(Guid.NewGuid(), "Monthly report", "Evidence", ReportType.Activity, Guid.NewGuid());

    public ReviewReportCommand Command(Guid reportId, string role, bool approved) => new()
    {
        ReportId = reportId,
        UserId = Guid.NewGuid(),
        ActorRole = role,
        IsApproved = approved,
        ReviewNote = approved ? null : "Please revise the evidence."
    };
}
