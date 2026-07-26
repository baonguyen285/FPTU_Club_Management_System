using System.Globalization;
using System.Text;
using Report.Application.DTOs;
using Report.Application.Interfaces;
using Report.Domain.Enums;

namespace Report.Application.Generation;

public sealed class RuleBasedReportDraftGenerator : IReportDraftGenerator
{
    public const string GeneratorType = "RuleBased";
    public const int MaximumListedItems = 10;
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private readonly TimeProvider _timeProvider;

    public RuleBasedReportDraftGenerator(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public Task<GeneratedReportDraftBody> GenerateAsync(
        ReportGenerationSnapshot snapshot,
        ReportType reportType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var title = BuildTitle(snapshot, reportType);
        var content = new StringBuilder();
        Introduction(content, snapshot, reportType);
        Membership(content, snapshot);
        Events(content, snapshot);
        Finance(content, snapshot);
        Kpi(content, snapshot);
        Limitations(content, snapshot);

        return Task.FromResult(new GeneratedReportDraftBody(
            title,
            content.ToString().TrimEnd(),
            snapshot.Sources,
            _timeProvider.GetUtcNow().UtcDateTime));
    }

    private static string BuildTitle(ReportGenerationSnapshot snapshot, ReportType reportType)
    {
        var category = reportType switch
        {
            ReportType.Financial => "tài chính",
            ReportType.Activity => "hoạt động",
            ReportType.General => "tổng hợp",
            _ => throw new ArgumentOutOfRangeException(nameof(reportType))
        };
        return $"Báo cáo {category} học kỳ {snapshot.SemesterCode} - {snapshot.ClubName}";
    }

    private static void Introduction(
        StringBuilder content, ReportGenerationSnapshot snapshot, ReportType reportType)
    {
        var purpose = reportType switch
        {
            ReportType.Financial => "tổng hợp dữ liệu tài chính",
            ReportType.Activity => "tổng hợp dữ liệu hoạt động",
            _ => "tổng hợp tình hình hoạt động"
        };
        content.AppendLine("## 1. Giới thiệu");
        content.AppendLine(
            $"Báo cáo này {purpose} của {snapshot.ClubName} trong học kỳ {snapshot.SemesterCode} dựa trên dữ liệu hiện có của hệ thống.");
        content.AppendLine();
    }

    private static void Membership(StringBuilder content, ReportGenerationSnapshot snapshot)
    {
        content.AppendLine("## 2. Thành viên");
        if (!snapshot.Availability.Membership
            || (!snapshot.TotalMembers.HasValue && !snapshot.NewMembers.HasValue))
        {
            content.AppendLine("Chưa có dữ liệu thành viên khả dụng cho lần tổng hợp này.");
        }
        else
        {
            if (snapshot.TotalMembers.HasValue)
                content.AppendLine(
                    $"Tại thời điểm tổng hợp, câu lạc bộ có {snapshot.TotalMembers.Value} thành viên approved và đang hoạt động theo snapshot hệ thống.");
            if (snapshot.NewMembers.HasValue)
                content.AppendLine(
                    $"Trong học kỳ có {snapshot.NewMembers.Value} thành viên mới được phê duyệt theo JoinedAt.");
        }
        content.AppendLine();
    }

    private static void Events(StringBuilder content, ReportGenerationSnapshot snapshot)
    {
        content.AppendLine("## 3. Hoạt động");
        if (!snapshot.Availability.Events)
        {
            content.AppendLine("Chưa có dữ liệu hoạt động khả dụng cho lần tổng hợp này.");
            content.AppendLine();
            return;
        }

        if (snapshot.CompletedEvents is > 0)
            content.AppendLine($"Hệ thống ghi nhận {snapshot.CompletedEvents.Value} hoạt động ở trạng thái Completed trong phạm vi snapshot học kỳ.");
        else
            content.AppendLine("Chưa ghi nhận hoạt động ở trạng thái hoàn thành trong học kỳ.");

        if (snapshot.CancelledEvents is > 0)
            content.AppendLine($"Có {snapshot.CancelledEvents.Value} hoạt động ở trạng thái Cancelled; các hoạt động này không được xem là thành tích hoàn thành.");

        var completed = snapshot.Events
            .Where(x => x.Status == "Completed")
            .OrderBy(x => x.ExpectedDate)
            .ThenBy(x => x.Id)
            .Take(MaximumListedItems)
            .ToList();
        if (completed.Count > 0)
        {
            content.AppendLine("Hoạt động Completed được ghi nhận:");
            foreach (var item in completed)
                content.AppendLine(
                    $"- {item.Title} — ngày dự kiến {FormatDate(item.ExpectedDate)} — trạng thái {item.Status}.");
        }
        content.AppendLine();
    }

    private static void Finance(StringBuilder content, ReportGenerationSnapshot snapshot)
    {
        content.AppendLine("## 4. Tài chính");
        if (!snapshot.Availability.Finance)
        {
            content.AppendLine("Chưa có dữ liệu tài chính khả dụng cho lần tổng hợp này.");
            content.AppendLine();
            return;
        }

        if (snapshot.ApprovedBudget.HasValue)
            content.AppendLine($"Ngân sách được duyệt theo snapshot: {FormatMoney(snapshot.ApprovedBudget.Value)}.");
        else
            content.AppendLine("Chưa có số liệu ngân sách được duyệt khả dụng.");

        if (snapshot.ActualExpense.HasValue)
            content.AppendLine($"Chi thực tế đã quyết toán theo snapshot: {FormatMoney(snapshot.ActualExpense.Value)}.");
        else
            content.AppendLine("Chưa có số liệu chi thực tế đã quyết toán khả dụng.");

        if (snapshot.RemainingBalance.HasValue)
            content.AppendLine(
                $"Số dư persisted hiện tại: {FormatMoney(snapshot.RemainingBalance.Value)}. Đây là số dư hiện tại của câu lạc bộ, không phải số dư riêng của học kỳ.");
        else
            content.AppendLine("Chưa có số dư persisted hiện tại khả dụng; không thay giá trị thiếu bằng 0.");

        var items = snapshot.FinanceItems
            .OrderBy(x => x.ProposedDate)
            .ThenBy(x => x.Id)
            .Take(MaximumListedItems)
            .ToList();
        if (items.Count > 0)
        {
            content.AppendLine("Đề xuất tài chính trong snapshot:");
            foreach (var item in items)
            {
                var amounts = new List<string> { $"đề nghị {FormatMoney(item.RequestedAmount)}" };
                if (item.ApprovedAmount.HasValue)
                    amounts.Add($"được duyệt {FormatMoney(item.ApprovedAmount.Value)}");
                if (item.ActualAmount.HasValue)
                    amounts.Add($"thực chi {FormatMoney(item.ActualAmount.Value)}");
                content.AppendLine(
                    $"- {item.Title} — {FormatDate(item.ProposedDate)} — {item.Status} — {string.Join(", ", amounts)}.");
            }
        }
        content.AppendLine();
    }

    private static void Kpi(StringBuilder content, ReportGenerationSnapshot snapshot)
    {
        content.AppendLine("## 5. KPI");
        if (snapshot.Availability.Kpi && snapshot.KpiScore.HasValue && snapshot.KpiRank.HasValue)
            content.AppendLine(
                $"KPI backend ghi nhận {FormatNumber(snapshot.KpiScore.Value)} điểm, xếp hạng {snapshot.KpiRank.Value} trong học kỳ.");
        else
            content.AppendLine("Chưa có dữ liệu KPI được ghi nhận cho học kỳ này.");
        content.AppendLine();
    }

    private static void Limitations(StringBuilder content, ReportGenerationSnapshot snapshot)
    {
        content.AppendLine("## 6. Ghi chú dữ liệu");
        content.AppendLine("- Số thành viên dựa trên membership approved/active hiện tại và JoinedAt; hệ thống chưa có lịch sử membership đầy đủ.");
        content.AppendLine("- Ngày của hoạt động là ExpectedDate; hệ thống chưa có completion timestamp riêng.");
        if (snapshot.RemainingBalance.HasValue)
            content.AppendLine("- RemainingBalance là số dư persisted hiện tại, không phải số dư riêng theo học kỳ.");
        if (!snapshot.KpiScore.HasValue || !snapshot.KpiRank.HasValue)
            content.AppendLine("- KPI chưa khả dụng được giữ là dữ liệu thiếu, không thay bằng 0 hoặc tự suy hạng.");
    }

    private static string FormatDate(DateTime value) =>
        value.ToUniversalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value) =>
        $"{value.ToString("#,0.##", VietnameseCulture)} VNĐ";

    private static string FormatNumber(decimal value) =>
        value.ToString("#,0.##", VietnameseCulture);
}
