using Report.Application.DTOs;

namespace Report.Application.Validation;

public static class ReportValidationPolicy
{
    public const int RecommendedMinimumContentLength = 100;
    public const string SnapshotVersion = "sra-1.v1";
}

public sealed record ReportValidationContext(
    ValidateReportRequest Request,
    ReportGenerationSnapshot Snapshot,
    DateTime EvaluatedAt);

public interface IReportValidationRule
{
    IReadOnlyList<ReportValidationIssue> Evaluate(ReportValidationContext context);
}

public interface IReportValidationEngine
{
    ReportValidationResult Validate(ValidateReportRequest request, ReportGenerationSnapshot snapshot);
}

public sealed class ReportValidationEngine : IReportValidationEngine
{
    private readonly IReadOnlyList<IReportValidationRule> _rules;
    private readonly TimeProvider _timeProvider;

    public ReportValidationEngine(IEnumerable<IReportValidationRule> rules, TimeProvider timeProvider)
    {
        _rules = rules.ToList();
        _timeProvider = timeProvider;
    }

    public ReportValidationResult Validate(ValidateReportRequest request, ReportGenerationSnapshot snapshot)
    {
        var evaluatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var context = new ReportValidationContext(request, snapshot, evaluatedAt);
        var issues = _rules
            .SelectMany(rule => rule.Evaluate(context))
            .DistinctBy(issue => new { issue.Code, issue.Field, issue.SourceType, issue.SourceId })
            .ToList();

        var errors = issues.Where(x => x.Severity == ValidationSeverity.Error).ToList();
        return new ReportValidationResult(
            errors.Count == 0,
            errors,
            issues.Where(x => x.Severity == ValidationSeverity.Warning).ToList(),
            issues.Where(x => x.Severity == ValidationSeverity.Suggestion).ToList(),
            evaluatedAt,
            request.ClubId,
            request.SemesterId,
            ReportValidationPolicy.SnapshotVersion,
            snapshot.Availability);
    }
}
