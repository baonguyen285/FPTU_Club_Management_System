using Report.Domain.Enums;
using Shared.Kernel.Exceptions;

namespace CommunicationReliability.Tests;

public sealed class ReportRevisionWorkflowTests
{
    [Fact]
    public void NewSemesterReport_StartsAsDraft_AndRequiresSubmit()
    {
        var report = NewReport();
        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.NotNull(report.SemesterId);

        report.Submit();
        Assert.Equal(ReportStatus.PendingApproval, report.Status);
    }

    [Fact]
    public void RequestRevision_RequiresFeedback()
    {
        var report = NewReport();
        report.Submit();
        Assert.Throws<Shared.Kernel.Exceptions.InvalidDomainException>(() =>
            report.RequestRevision(Guid.NewGuid(), " "));
    }

    [Fact]
    public void Revision_CanBeEditedAndResubmitted_WithIncrementedRevision()
    {
        var report = NewReport();
        report.Submit();
        report.RequestRevision(Guid.NewGuid(), "Add evidence");
        report.Update("Updated report", "Updated evidence", ReportType.Activity);
        report.Submit();

        Assert.Equal(ReportStatus.PendingApproval, report.Status);
        Assert.Equal(2, report.RevisionNumber);
    }

    [Fact]
    public void PendingReport_CannotBeSubmittedTwice()
    {
        var report = NewReport();
        report.Submit();
        Assert.Throws<ConflictException>(report.Submit);
    }

    [Fact]
    public void ApprovedReport_CannotBeEdited()
    {
        var report = NewReport();
        report.Submit();
        report.Approve(Guid.NewGuid());
        Assert.Throws<ConflictException>(() =>
            report.Update("Changed", "Changed", ReportType.General));
    }

    private static Report.Domain.Entities.Report NewReport() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Monthly report", "Evidence", ReportType.Activity, Guid.NewGuid());
}
