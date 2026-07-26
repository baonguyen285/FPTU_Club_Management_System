using Report.Application.DTOs;
using Report.Domain.Enums;

namespace Report.Application.Validation;

public sealed class ReportCompletenessRule : IReportValidationRule
{
    public IReadOnlyList<ReportValidationIssue> Evaluate(ReportValidationContext context)
    {
        var request = context.Request;
        var issues = new List<ReportValidationIssue>();
        if (request.ClubId == Guid.Empty) issues.Add(Error("REPORT_CLUB_REQUIRED", "ClubId là bắt buộc.", "clubId"));
        if (request.SemesterId == Guid.Empty) issues.Add(Error("REPORT_SEMESTER_REQUIRED", "SemesterId là bắt buộc.", "semesterId"));
        if (!Enum.IsDefined(request.ReportType) || request.ReportType == 0)
            issues.Add(Error("REPORT_TYPE_REQUIRED", "Loại báo cáo là bắt buộc.", "reportType"));
        if (string.IsNullOrWhiteSpace(request.Title))
            issues.Add(Error("REPORT_TITLE_REQUIRED", "Tiêu đề báo cáo là bắt buộc.", "title"));
        if (string.IsNullOrWhiteSpace(request.Content))
            issues.Add(Error("REPORT_CONTENT_REQUIRED", "Nội dung báo cáo là bắt buộc.", "content"));
        else if (request.Content.Trim().Length < ReportValidationPolicy.RecommendedMinimumContentLength)
            issues.Add(new ReportValidationIssue(
                "REPORT_CONTENT_TOO_SHORT", ValidationSeverity.Warning,
                $"Nội dung báo cáo ngắn hơn mức khuyến nghị {ReportValidationPolicy.RecommendedMinimumContentLength} ký tự.",
                "content", SuggestedAction: "Bổ sung mô tả kết quả, số liệu và nguồn minh chứng phù hợp."));
        return issues;
    }

    private static ReportValidationIssue Error(string code, string message, string field) =>
        new(code, ValidationSeverity.Error, message, field);
}

public sealed class EventConsistencyRule : IReportValidationRule
{
    public IReadOnlyList<ReportValidationIssue> Evaluate(ReportValidationContext context)
    {
        var issues = new List<ReportValidationIssue>();
        foreach (var item in context.Snapshot.Events.OrderBy(x => x.ExpectedDate).ThenBy(x => x.Id))
        {
            if (item.Status == "Cancelled")
                issues.Add(Issue(
                    "EVENT_CANCELLED_EXCLUDED", ValidationSeverity.Suggestion, item,
                    "Sự kiện đã hủy được loại khỏi chỉ số hoạt động hoàn thành.",
                    "Không mô tả sự kiện này như một hoạt động đã hoàn thành."));
            else if (item.ExpectedDate < context.EvaluatedAt
                     && item.Status is not "Completed" and not "Cancelled")
                issues.Add(Issue(
                    "EVENT_PAST_DUE_NOT_COMPLETED", ValidationSeverity.Warning, item,
                    "Sự kiện đã qua ngày dự kiến nhưng chưa ở trạng thái Completed hoặc Cancelled.",
                    "Kiểm tra trạng thái sự kiện trước khi mô tả kết quả."));
        }
        return issues;
    }

    private static ReportValidationIssue Issue(
        string code, ValidationSeverity severity, ReportSnapshotEvent item,
        string message, string action) =>
        new(code, severity, message, SourceType: "Event", SourceId: item.Id.ToString(),
            SourceTitle: item.Title, SuggestedAction: action);
}

public sealed class FinanceConsistencyRule : IReportValidationRule
{
    public IReadOnlyList<ReportValidationIssue> Evaluate(ReportValidationContext context)
    {
        var issues = new List<ReportValidationIssue>();
        if (!context.Snapshot.Availability.Finance)
        {
            issues.Add(new ReportValidationIssue(
                "FINANCE_DATA_UNAVAILABLE", ValidationSeverity.Warning,
                "Dữ liệu tài chính không khả dụng cho lần kiểm tra này.",
                SourceType: "Finance",
                SuggestedAction: "Thử lại khi Finance service khả dụng."));
            return issues;
        }

        foreach (var item in context.Snapshot.FinanceItems.OrderBy(x => x.ProposedDate).ThenBy(x => x.Id))
        {
            if (item.ActualAmount.HasValue && item.ApprovedAmount.HasValue
                && item.ActualAmount.Value > item.ApprovedAmount.Value)
                issues.Add(Issue(
                    "FINANCE_ACTUAL_EXCEEDS_APPROVED", ValidationSeverity.Error, item,
                    "Chi thực tế vượt số tiền được phê duyệt trên cùng đề xuất.",
                    "Đối chiếu quyết toán và số tiền phê duyệt."));

            if (item.Status is "Approved" or "PartiallyApproved")
                issues.Add(Issue(
                    "FINANCE_UNSETTLED_APPROVED_PROPOSAL", ValidationSeverity.Warning, item,
                    "Đề xuất đã được phê duyệt nhưng chưa quyết toán.",
                    "Hoàn tất quyết toán hoặc ghi rõ đây là khoản chưa quyết toán."));

            if (item.Status is "Draft" or "Pending" or "Rejected")
                issues.Add(Issue(
                    "FINANCE_UNAPPROVED_EXCLUDED", ValidationSeverity.Suggestion, item,
                    "Đề xuất chưa được phê duyệt được loại khỏi tổng ngân sách được duyệt.",
                    "Không cộng khoản này vào ngân sách được phê duyệt."));
        }
        return issues;
    }

    private static ReportValidationIssue Issue(
        string code, ValidationSeverity severity, ReportSnapshotFinanceItem item,
        string message, string action) =>
        new(code, severity, message, SourceType: "Finance", SourceId: item.Id.ToString(),
            SourceTitle: item.Title, SuggestedAction: action);
}

public sealed class MembershipLimitationRule : IReportValidationRule
{
    public IReadOnlyList<ReportValidationIssue> Evaluate(ReportValidationContext context) =>
    [
        new(
            "MEMBERSHIP_DATA_LIMITED",
            ValidationSeverity.Suggestion,
            "Số thành viên dựa trên trạng thái approved hiện tại và JoinedAt; hệ thống chưa có lịch sử khoảng hiệu lực membership.",
            SourceType: "Membership",
            SuggestedAction: "Trình bày số thành viên như snapshot hiện tại, không khẳng định headcount lịch sử tuyệt đối.")
    ];
}

public sealed class KpiAvailabilityRule : IReportValidationRule
{
    public IReadOnlyList<ReportValidationIssue> Evaluate(ReportValidationContext context)
    {
        if (context.Snapshot.Availability.Kpi
            && context.Snapshot.KpiScore.HasValue
            && context.Snapshot.KpiRank.HasValue)
            return [];

        return
        [
            new(
                "KPI_UNAVAILABLE",
                ValidationSeverity.Suggestion,
                "KPI score hoặc rank chưa có trong ledger/leaderboard backend.",
                SourceType: "KPI",
                SuggestedAction: "Không thay giá trị thiếu bằng 0 và không tự suy ra thứ hạng.")
        ];
    }
}
