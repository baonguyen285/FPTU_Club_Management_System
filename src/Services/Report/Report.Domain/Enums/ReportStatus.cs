namespace Report.Domain.Enums
{
    public enum ReportStatus
    {
        Draft = 0,
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3,
        RequestRevision = 4,
        Pending = PendingApproval
    }
}
