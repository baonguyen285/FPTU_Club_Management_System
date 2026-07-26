using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Report.API.Controllers;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Application.Validation;
using Report.Domain.Enums;
using Report.Infrastructure.Services;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class SmartReportValidationTests
{
    private static readonly Guid ClubId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SemesterId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ActorId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly DateTime Now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MissingRequiredFields_AreBlockingErrors()
    {
        var result = Engine().Validate(new ValidateReportRequest(), Snapshot());

        Assert.False(result.IsReadyToSubmit);
        Assert.Equal(
            ["REPORT_CLUB_REQUIRED", "REPORT_SEMESTER_REQUIRED", "REPORT_TYPE_REQUIRED", "REPORT_TITLE_REQUIRED", "REPORT_CONTENT_REQUIRED"],
            result.Errors.Select(x => x.Code));
    }

    [Fact]
    public void ShortContent_IsWarning_AndDoesNotBlock()
    {
        var result = Validate(Snapshot(), content: "Nội dung ngắn");
        Assert.True(result.IsReadyToSubmit);
        Assert.Contains(result.Warnings, x => x.Code == "REPORT_CONTENT_TOO_SHORT");
    }

    [Fact]
    public void CancelledEvent_IsExplicitlyExcluded()
    {
        var cancelled = Event("Đã hủy", Now.AddDays(-5), "Cancelled");
        var result = Validate(Snapshot(events: [cancelled]));
        var issue = Assert.Single(result.Suggestions, x => x.Code == "EVENT_CANCELLED_EXCLUDED");
        Assert.Equal(cancelled.Id.ToString(), issue.SourceId);
    }

    [Fact]
    public void PastDueIncompleteEvent_IsWarning()
    {
        var result = Validate(Snapshot(events: [Event("Quá hạn", Now.AddDays(-1), "Approved")]));
        Assert.Contains(result.Warnings, x => x.Code == "EVENT_PAST_DUE_NOT_COMPLETED");
        Assert.True(result.IsReadyToSubmit);
    }

    [Fact]
    public void FutureOrCompletedEvent_DoesNotProducePastDueWarning()
    {
        var result = Validate(Snapshot(events:
        [
            Event("Tương lai", Now.AddDays(1), "Approved"),
            Event("Hoàn thành", Now.AddDays(-1), "Completed")
        ]));
        Assert.DoesNotContain(result.Warnings, x => x.Code == "EVENT_PAST_DUE_NOT_COMPLETED");
    }

    [Fact]
    public void ActualExpenseAboveApproved_IsBlockingError()
    {
        var result = Validate(Snapshot(finance:
        [
            Finance("Vượt mức", "Settled", approved: 100, actual: 120)
        ]));
        Assert.Contains(result.Errors, x => x.Code == "FINANCE_ACTUAL_EXCEEDS_APPROVED");
        Assert.False(result.IsReadyToSubmit);
    }

    [Fact]
    public void ApprovedButUnsettled_IsWarning()
    {
        var result = Validate(Snapshot(finance:
        [
            Finance("Chưa quyết toán", "Approved", approved: 100)
        ]));
        Assert.Contains(result.Warnings, x => x.Code == "FINANCE_UNSETTLED_APPROVED_PROPOSAL");
        Assert.True(result.IsReadyToSubmit);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    public void UnapprovedProposal_IsMarkedExcluded(string status)
    {
        var result = Validate(Snapshot(finance: [Finance("Không duyệt", status)]));
        Assert.Contains(result.Suggestions, x => x.Code == "FINANCE_UNAPPROVED_EXCLUDED");
    }

    [Fact]
    public void FinanceUnavailable_ProducesWarningWithoutFakeMetrics()
    {
        var snapshot = Snapshot(availability: Availability(finance: false, balance: false));
        var result = Validate(snapshot);
        Assert.Contains(result.Warnings, x => x.Code == "FINANCE_DATA_UNAVAILABLE");
        Assert.Null(snapshot.ApprovedBudget);
        Assert.Null(snapshot.ActualExpense);
    }

    [Fact]
    public void MembershipHistoryLimitation_IsSuggestion()
    {
        var result = Validate(Snapshot());
        Assert.Contains(result.Suggestions, x => x.Code == "MEMBERSHIP_DATA_LIMITED");
    }

    [Fact]
    public void KpiUnavailable_IsSuggestion_AndRemainsNull()
    {
        var snapshot = Snapshot(kpiScore: null, kpiRank: null);
        var result = Validate(snapshot);
        Assert.Contains(result.Suggestions, x => x.Code == "KPI_UNAVAILABLE");
        Assert.Null(snapshot.KpiScore);
        Assert.Null(snapshot.KpiRank);
    }

    [Fact]
    public void WarningOnly_ResultIsReadyToSubmit()
    {
        var result = Validate(Snapshot(events: [Event("Quá hạn", Now.AddDays(-1), "Approved")]));
        Assert.Empty(result.Errors);
        Assert.True(result.IsReadyToSubmit);
    }

    [Fact]
    public void RuleOrderAndIssueOrder_AreDeterministic()
    {
        var snapshot = Snapshot(
            events:
            [
                Event("Sau", Now.AddDays(-1), "Approved", Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")),
                Event("Trước", Now.AddDays(-2), "Approved", Guid.Parse("00000000-0000-0000-0000-000000000001"))
            ],
            finance:
            [
                Finance("Hai", "Approved", approved: 2, id: Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")),
                Finance("Một", "Approved", approved: 1, id: Guid.Parse("00000000-0000-0000-0000-000000000001"))
            ]);
        var engine = Engine();

        var first = engine.Validate(Request(), snapshot);
        var second = engine.Validate(Request(), snapshot);

        Assert.Equal(first.Warnings.Select(Key), second.Warnings.Select(Key));
        Assert.Equal(first.Suggestions.Select(Key), second.Suggestions.Select(Key));
    }

    [Fact]
    public void DuplicateRuleOutput_IsDeduplicatedByStableIdentity()
    {
        var duplicateRule = new DuplicateRule();
        var engine = new ReportValidationEngine(
            [duplicateRule, duplicateRule], new FixedTimeProvider(Now));
        var result = engine.Validate(Request(), Snapshot());
        Assert.Single(result.Warnings, x => x.Code == "DUPLICATE_TEST");
    }

    [Fact]
    public async Task ValidationService_FetchesSnapshotExactlyOnce()
    {
        var snapshot = Snapshot();
        var source = new Mock<ISmartReportSnapshotService>();
        source.Setup(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var service = new ReportValidationService(source.Object, Engine());

        await service.ValidateAsync(Request());

        source.Verify(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidationService_PropagatesDependencyUnavailable()
    {
        var source = new Mock<ISmartReportSnapshotService>();
        source.Setup(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("dependency unavailable"));
        var service = new ReportValidationService(source.Object, Engine());

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ValidateAsync(Request()));
    }

    [Fact]
    public async Task ValidationService_PropagatesSemesterNotFound()
    {
        var source = new Mock<ISmartReportSnapshotService>();
        source.Setup(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Semester not found."));
        var service = new ReportValidationService(source.Object, Engine());

        await Assert.ThrowsAsync<NotFoundException>(() => service.ValidateAsync(Request()));
    }

    [Fact]
    public async Task Endpoint_ApprovedLeaderForExactClub_Returns200()
    {
        var controller = Controller(SystemRoleNames.Student, allowed: true);
        Assert.IsType<OkObjectResult>(await controller.ValidateSmartAssistant(Request(), default));
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Treasurer")]
    public async Task Endpoint_ActorWithoutLeaderCapability_Returns403(string role)
    {
        var controller = Controller(role, allowed: false);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => controller.ValidateSmartAssistant(Request(), default));
    }

    [Fact]
    public async Task Endpoint_CrossClubLeader_Returns403()
    {
        var controller = Controller(SystemRoleNames.Student, allowed: false);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => controller.ValidateSmartAssistant(Request(), default));
    }

    [Fact]
    public async Task Endpoint_AdminValidationReadPolicy_Returns200WithoutCapabilityCall()
    {
        var club = new Mock<IClubGrpcClient>(MockBehavior.Strict);
        var controller = Controller(SystemRoleNames.StudentAffairsAdmin, false, club);
        Assert.IsType<OkObjectResult>(await controller.ValidateSmartAssistant(Request(), default));
        club.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Endpoint_EmptyIdentifiers_Return400ContractException()
    {
        var controller = Controller(SystemRoleNames.Student, allowed: true);
        await Assert.ThrowsAsync<BadRequestException>(
            () => controller.ValidateSmartAssistant(new ValidateReportRequest(), default));
    }

    private static ReportValidationEngine Engine() => new(
        [
            new ReportCompletenessRule(),
            new EventConsistencyRule(),
            new FinanceConsistencyRule(),
            new MembershipLimitationRule(),
            new KpiAvailabilityRule()
        ],
        new FixedTimeProvider(Now));

    private static ReportValidationResult Validate(
        ReportGenerationSnapshot snapshot, string? content = null) =>
        Engine().Validate(Request(content), snapshot);

    private static ValidateReportRequest Request(string? content = null) => new()
    {
        ClubId = ClubId,
        SemesterId = SemesterId,
        ReportType = ReportType.General,
        Title = "Báo cáo học kỳ",
        Content = content ?? new string('x', ReportValidationPolicy.RecommendedMinimumContentLength)
    };

    private static ReportGenerationSnapshot Snapshot(
        IReadOnlyList<ReportSnapshotEvent>? events = null,
        IReadOnlyList<ReportSnapshotFinanceItem>? finance = null,
        decimal? kpiScore = 10,
        int? kpiRank = 1,
        ReportSnapshotAvailability? availability = null) =>
        new(
            ClubId, "Club", SemesterId, "SUMMER-2026", 10, 2,
            events?.Count(x => x.Status == "Completed") ?? 0,
            events?.Count(x => x.Status == "Cancelled") ?? 0,
            finance is { Count: > 0 } ? finance.Where(x => x.Status is "Approved" or "PartiallyApproved" or "Settled").Sum(x => x.ApprovedAmount ?? 0) : null,
            finance is { Count: > 0 } ? finance.Where(x => x.Status == "Settled").Sum(x => x.ActualAmount ?? 0) : null,
            null, kpiScore, kpiRank, events ?? [], finance ?? [], [],
            availability ?? Availability());

    private static ReportSnapshotAvailability Availability(bool finance = true, bool balance = false) =>
        new(true, true, true, finance, balance, true);

    private static ReportSnapshotEvent Event(
        string title, DateTime date, string status, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), title, date, status);

    private static ReportSnapshotFinanceItem Finance(
        string title, string status, decimal? approved = null, decimal? actual = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), null, title, Now.AddDays(-10), status, 100, approved, actual);

    private static string Key(ReportValidationIssue issue) =>
        $"{issue.Code}|{issue.SourceType}|{issue.SourceId}";

    private static ReportsController Controller(
        string role, bool allowed, Mock<IClubGrpcClient>? clubMock = null)
    {
        var club = clubMock ?? new Mock<IClubGrpcClient>();
        if (clubMock is null)
            club.Setup(x => x.CanSubmitReportsAsync(ClubId, ActorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allowed);
        var validation = new Mock<IReportValidationService>();
        validation.Setup(x => x.ValidateAsync(It.IsAny<ValidateReportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Validate(Snapshot()));
        var controller = new ReportsController(
            Mock.Of<IMediator>(), club.Object, Mock.Of<ISmartReportSnapshotService>(), validation.Object,
            Mock.Of<IReportDraftGenerationService>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(JwtRegisteredClaimNames.Sub, ActorId.ToString()), new Claim("role", role)],
                    "test", JwtRegisteredClaimNames.Name, "role"))
            }
        };
        return controller;
    }

    private sealed class FixedTimeProvider(DateTime value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(value);
    }

    private sealed class DuplicateRule : IReportValidationRule
    {
        public IReadOnlyList<ReportValidationIssue> Evaluate(ReportValidationContext context) =>
        [
            new("DUPLICATE_TEST", ValidationSeverity.Warning, "duplicate", SourceType: "Test", SourceId: "1")
        ];
    }
}
