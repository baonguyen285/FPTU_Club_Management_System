using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Report.API.Controllers;
using Report.Application.DTOs;
using Report.Application.Generation;
using Report.Application.Interfaces;
using Report.Application.Validation;
using Report.Domain.Enums;
using Report.Infrastructure.Services;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class SmartReportDraftGenerationTests
{
    private static readonly Guid ClubId = Guid.Parse("41000000-0000-0000-0000-000000000001");
    private static readonly Guid SemesterId = Guid.Parse("42000000-0000-0000-0000-000000000002");
    private static readonly Guid ActorId = Guid.Parse("43000000-0000-0000-0000-000000000003");
    private static readonly DateTime Now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ReportType.Financial, "Báo cáo tài chính học kỳ SUMMER-2026 - Câu lạc bộ Công nghệ")]
    [InlineData(ReportType.Activity, "Báo cáo hoạt động học kỳ SUMMER-2026 - Câu lạc bộ Công nghệ")]
    [InlineData(ReportType.General, "Báo cáo tổng hợp học kỳ SUMMER-2026 - Câu lạc bộ Công nghệ")]
    public async Task FullSnapshot_GeneratesExpectedTitleAndSections(ReportType type, string title)
    {
        var body = await Generator().GenerateAsync(FullSnapshot(), type);
        Assert.Equal(title, body.GeneratedTitle);
        Assert.Contains("## 1. Giới thiệu", body.GeneratedContent);
        Assert.Contains("## 2. Thành viên", body.GeneratedContent);
        Assert.Contains("## 3. Hoạt động", body.GeneratedContent);
        Assert.Contains("## 4. Tài chính", body.GeneratedContent);
        Assert.Contains("## 5. KPI", body.GeneratedContent);
        Assert.Contains("## 6. Ghi chú dữ liệu", body.GeneratedContent);
    }

    [Fact]
    public async Task SameInput_ProducesSameTitleAndContent()
    {
        var generator = Generator();
        var first = await generator.GenerateAsync(FullSnapshot(), ReportType.General);
        var second = await generator.GenerateAsync(FullSnapshot(), ReportType.General);
        Assert.Equal(first.GeneratedTitle, second.GeneratedTitle);
        Assert.Equal(first.GeneratedContent, second.GeneratedContent);
    }

    [Fact]
    public async Task Events_AreOrderedByExpectedDateThenId()
    {
        var early = Event("Sớm", Now.AddDays(-3), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var sameFirst = Event("Cùng ngày ID nhỏ", Now.AddDays(-1), Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var sameSecond = Event("Cùng ngày ID lớn", Now.AddDays(-1), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var content = (await Generator().GenerateAsync(
            Snapshot(events: [sameSecond, sameFirst, early]), ReportType.Activity)).GeneratedContent;
        Assert.True(content.IndexOf(early.Title, StringComparison.Ordinal) < content.IndexOf(sameFirst.Title, StringComparison.Ordinal));
        Assert.True(content.IndexOf(sameFirst.Title, StringComparison.Ordinal) < content.IndexOf(sameSecond.Title, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FinanceItems_AreOrderedByProposedDateThenId()
    {
        var first = Finance("Khoản một", Now.AddDays(-3), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var sameFirst = Finance("Khoản hai A", Now.AddDays(-1), Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var sameSecond = Finance("Khoản hai B", Now.AddDays(-1), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var content = (await Generator().GenerateAsync(
            Snapshot(finance: [sameSecond, sameFirst, first]), ReportType.Financial)).GeneratedContent;
        Assert.True(content.IndexOf(first.Title, StringComparison.Ordinal) < content.IndexOf(sameFirst.Title, StringComparison.Ordinal));
        Assert.True(content.IndexOf(sameFirst.Title, StringComparison.Ordinal) < content.IndexOf(sameSecond.Title, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoCompletedEvent_UsesNeutralMessage()
    {
        var body = await Generator().GenerateAsync(
            Snapshot(events: [new(Guid.NewGuid(), "Đã hủy", Now.AddDays(-1), "Cancelled")]),
            ReportType.Activity);
        Assert.Contains("Chưa ghi nhận hoạt động ở trạng thái hoàn thành", body.GeneratedContent);
        Assert.DoesNotContain("Hoạt động Completed được ghi nhận:", body.GeneratedContent);
    }

    [Fact]
    public async Task KpiUnavailable_DoesNotInventZeroOrRank()
    {
        var body = await Generator().GenerateAsync(
            Snapshot(kpiScore: null, kpiRank: null), ReportType.General);
        Assert.Contains("Chưa có dữ liệu KPI được ghi nhận", body.GeneratedContent);
        Assert.DoesNotContain("KPI backend ghi nhận 0", body.GeneratedContent);
        Assert.DoesNotContain("xếp hạng 0", body.GeneratedContent);
    }

    [Fact]
    public async Task FinanceUnavailable_DoesNotCreateNumbers()
    {
        var snapshot = Snapshot(
            approvedBudget: null, actualExpense: null, remainingBalance: null,
            availability: new(true, true, true, false, false, true));
        var body = await Generator().GenerateAsync(snapshot, ReportType.Financial);
        Assert.Contains("Chưa có dữ liệu tài chính khả dụng", body.GeneratedContent);
        Assert.DoesNotContain("0 VNĐ", body.GeneratedContent);
    }

    [Fact]
    public async Task MembershipText_StatesCurrentSnapshotLimitation()
    {
        var content = (await Generator().GenerateAsync(FullSnapshot(), ReportType.General)).GeneratedContent;
        Assert.Contains("approved/active hiện tại", content);
        Assert.Contains("chưa có lịch sử membership đầy đủ", content);
    }

    [Fact]
    public async Task RemainingBalance_IsExplicitlyCurrentNotSemesterBalance()
    {
        var content = (await Generator().GenerateAsync(FullSnapshot(), ReportType.Financial)).GeneratedContent;
        Assert.Contains("Số dư persisted hiện tại", content);
        Assert.Contains("không phải số dư riêng của học kỳ", content);
    }

    [Fact]
    public async Task Generator_DoesNotInventParticipantCountEvidenceOrCompletionDate()
    {
        var content = (await Generator().GenerateAsync(FullSnapshot(), ReportType.Activity)).GeneratedContent;
        Assert.DoesNotContain("người tham gia", content);
        Assert.DoesNotContain("minh chứng", content);
        Assert.DoesNotContain("ngày hoàn thành", content);
        Assert.Contains("ngày dự kiến", content);
    }

    [Fact]
    public async Task NullMetrics_AreNotReplacedWithZero()
    {
        var content = (await Generator().GenerateAsync(
            Snapshot(totalMembers: null, newMembers: null, approvedBudget: null,
                actualExpense: null, remainingBalance: null, kpiScore: null, kpiRank: null),
            ReportType.General)).GeneratedContent;
        Assert.Contains("Chưa có dữ liệu thành viên khả dụng", content);
        Assert.Contains("Chưa có số liệu ngân sách được duyệt khả dụng", content);
        Assert.DoesNotContain("0 VNĐ", content);
    }

    [Fact]
    public async Task MoneyFormatting_IsExplicitVietnameseAndStable()
    {
        var content = (await Generator().GenerateAsync(
            Snapshot(approvedBudget: 1200.5m, actualExpense: 1000m), ReportType.Financial)).GeneratedContent;
        Assert.Contains("1.200,5 VNĐ", content);
        Assert.Contains("1.000 VNĐ", content);
    }

    [Fact]
    public async Task Sources_AreReusedWithoutMutation()
    {
        var sources = new[]
        {
            new SourceReference("Club", ClubId.ToString(), "CLB"),
            new SourceReference("Semester", SemesterId.ToString(), "SUMMER-2026")
        };
        var body = await Generator().GenerateAsync(Snapshot(sources: sources), ReportType.General);
        Assert.Same(sources, body.Sources);
    }

    [Fact]
    public async Task UnicodeVietnamese_IsPreserved()
    {
        var body = await Generator().GenerateAsync(FullSnapshot(), ReportType.General);
        Assert.Contains("Câu lạc bộ Công nghệ", body.GeneratedTitle);
        Assert.Contains("Tài chính", body.GeneratedContent);
        Assert.DoesNotContain("Ã", body.GeneratedContent);
    }

    [Fact]
    public async Task Orchestrator_FetchesSnapshotOnce_AndValidatesGeneratedPayload()
    {
        var snapshot = FullSnapshot();
        var source = new Mock<ISmartReportSnapshotService>();
        source.Setup(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var validator = new Mock<IReportValidationEngine>();
        validator.Setup(x => x.Validate(It.IsAny<ValidateReportRequest>(), snapshot))
            .Returns((ValidateReportRequest request, ReportGenerationSnapshot _) =>
                Validation(request, snapshot.Availability));
        var service = new ReportDraftGenerationService(source.Object, Generator(), validator.Object);

        var result = await service.GenerateAsync(Request());

        source.Verify(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()), Times.Once);
        validator.Verify(x => x.Validate(
            It.Is<ValidateReportRequest>(request =>
                request.Title == result.GeneratedTitle
                && request.Content == result.GeneratedContent
                && request.ReportType == ReportType.General),
            snapshot), Times.Once);
        Assert.Same(result.Validation.Availability, snapshot.Availability);
    }

    [Fact]
    public async Task Orchestrator_ReturnsRuleBasedContractAndSnapshotVersion()
    {
        var service = Service(FullSnapshot());
        var result = await service.GenerateAsync(Request());
        Assert.Equal("RuleBased", result.GeneratorType);
        Assert.Equal("sra-1.v1", result.SnapshotVersion);
        Assert.Equal(ClubId, result.ClubId);
        Assert.Equal(SemesterId, result.SemesterId);
    }

    [Fact]
    public async Task Orchestrator_PropagatesSemesterMissing()
    {
        var source = new Mock<ISmartReportSnapshotService>();
        source.Setup(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Semester not found."));
        var service = new ReportDraftGenerationService(
            source.Object, Generator(), Mock.Of<IReportValidationEngine>());
        await Assert.ThrowsAsync<NotFoundException>(() => service.GenerateAsync(Request()));
    }

    [Fact]
    public async Task Orchestrator_PropagatesDependencyUnavailableWithoutFakeDraft()
    {
        var source = new Mock<ISmartReportSnapshotService>();
        source.Setup(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("dependency unavailable"));
        var service = new ReportDraftGenerationService(
            source.Object, Generator(), Mock.Of<IReportValidationEngine>());
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.GenerateAsync(Request()));
    }

    [Fact]
    public async Task InvalidReportType_ReturnsBadRequestBeforeSnapshot()
    {
        var source = new Mock<ISmartReportSnapshotService>(MockBehavior.Strict);
        var service = new ReportDraftGenerationService(
            source.Object, Generator(), Mock.Of<IReportValidationEngine>());
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.GenerateAsync(new GenerateReportDraftRequest
            {
                ClubId = ClubId, SemesterId = SemesterId, ReportType = (ReportType)99
            }));
        source.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Endpoint_ApprovedLeaderForExactClub_Returns200()
    {
        var controller = Controller(SystemRoleNames.Student, true);
        Assert.IsType<OkObjectResult>(
            await controller.GenerateSmartAssistantDraft(Request(), default));
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Treasurer")]
    public async Task Endpoint_ActorWithoutLeaderCapability_Returns403(string role)
    {
        var controller = Controller(role, false);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => controller.GenerateSmartAssistantDraft(Request(), default));
    }

    [Fact]
    public async Task Endpoint_CrossClubLeader_Returns403()
    {
        var controller = Controller(SystemRoleNames.Student, false);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => controller.GenerateSmartAssistantDraft(Request(), default));
    }

    [Fact]
    public async Task Endpoint_AdminPolicy_Returns200WithoutMembershipCall()
    {
        var club = new Mock<IClubGrpcClient>(MockBehavior.Strict);
        var controller = Controller(SystemRoleNames.StudentAffairsAdmin, false, club);
        Assert.IsType<OkObjectResult>(
            await controller.GenerateSmartAssistantDraft(Request(), default));
        club.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Endpoint_InvalidReportType_Returns400ContractException()
    {
        var controller = Controller(SystemRoleNames.Student, true);
        var request = Request();
        request.ReportType = (ReportType)99;
        await Assert.ThrowsAsync<BadRequestException>(
            () => controller.GenerateSmartAssistantDraft(request, default));
    }

    private static RuleBasedReportDraftGenerator Generator() =>
        new(new FixedTimeProvider(Now));

    private static IReportDraftGenerationService Service(ReportGenerationSnapshot snapshot)
    {
        var source = new Mock<ISmartReportSnapshotService>();
        source.Setup(x => x.GetPreviewAsync(ClubId, SemesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var validator = new Mock<IReportValidationEngine>();
        validator.Setup(x => x.Validate(It.IsAny<ValidateReportRequest>(), snapshot))
            .Returns((ValidateReportRequest request, ReportGenerationSnapshot _) =>
                Validation(request, snapshot.Availability));
        return new ReportDraftGenerationService(source.Object, Generator(), validator.Object);
    }

    private static ReportValidationResult Validation(
        ValidateReportRequest request,
        ReportSnapshotAvailability? availability = null) =>
        new(true, [], [], [], Now, request.ClubId, request.SemesterId, "sra-1.v1",
            availability ?? FullSnapshot().Availability);

    private static GenerateReportDraftRequest Request() => new()
    {
        ClubId = ClubId, SemesterId = SemesterId, ReportType = ReportType.General
    };

    private static ReportGenerationSnapshot FullSnapshot() => Snapshot(
        events:
        [
            Event("Ngày hội Công nghệ", Now.AddDays(-5), Guid.NewGuid()),
            new(Guid.NewGuid(), "Sự kiện đã hủy", Now.AddDays(-4), "Cancelled")
        ],
        finance:
        [
            new(Guid.NewGuid(), null, "Ngân sách ngày hội", Now.AddDays(-6), "Settled", 1500, 1200, 1000)
        ],
        totalMembers: 20, newMembers: 5,
        approvedBudget: 1200, actualExpense: 1000, remainingBalance: 5000,
        kpiScore: 85.5m, kpiRank: 2,
        sources:
        [
            new("Club", ClubId.ToString(), "Câu lạc bộ Công nghệ"),
            new("Semester", SemesterId.ToString(), "SUMMER-2026")
        ]);

    private static ReportGenerationSnapshot Snapshot(
        IReadOnlyList<ReportSnapshotEvent>? events = null,
        IReadOnlyList<ReportSnapshotFinanceItem>? finance = null,
        int? totalMembers = 1,
        int? newMembers = 1,
        decimal? approvedBudget = null,
        decimal? actualExpense = null,
        decimal? remainingBalance = null,
        decimal? kpiScore = 10,
        int? kpiRank = 1,
        IReadOnlyList<SourceReference>? sources = null,
        ReportSnapshotAvailability? availability = null) =>
        new(
            ClubId, "Câu lạc bộ Công nghệ", SemesterId, "SUMMER-2026",
            totalMembers, newMembers,
            events?.Count(x => x.Status == "Completed") ?? 0,
            events?.Count(x => x.Status == "Cancelled") ?? 0,
            approvedBudget, actualExpense, remainingBalance, kpiScore, kpiRank,
            events ?? [], finance ?? [], sources ?? [],
            availability ?? new(true, true, true, true, remainingBalance.HasValue, true));

    private static ReportSnapshotEvent Event(string title, DateTime date, Guid id) =>
        new(id, title, date, "Completed");

    private static ReportSnapshotFinanceItem Finance(string title, DateTime date, Guid id) =>
        new(id, null, title, date, "Approved", 100, 80, null);

    private static ReportsController Controller(
        string role, bool allowed, Mock<IClubGrpcClient>? clubMock = null)
    {
        var club = clubMock ?? new Mock<IClubGrpcClient>();
        if (clubMock is null)
            club.Setup(x => x.CanSubmitReportsAsync(ClubId, ActorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allowed);
        var generation = new Mock<IReportDraftGenerationService>();
        generation.Setup(x => x.GenerateAsync(It.IsAny<GenerateReportDraftRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedReportDraft(
                ClubId, SemesterId, ReportType.General, "Title", "Content", [],
                Validation(new ValidateReportRequest
                {
                    ClubId = ClubId, SemesterId = SemesterId, ReportType = ReportType.General
                }),
                "RuleBased", "sra-1.v1", Now));
        var controller = new ReportsController(
            Mock.Of<IMediator>(), club.Object, Mock.Of<ISmartReportSnapshotService>(),
            Mock.Of<IReportValidationService>(), generation.Object);
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
}
