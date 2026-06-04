using System;
using Report.Domain.Enums;
using Shared.Kernel.Exceptions;

namespace Report.Domain.Entities
{
    public class Report
    {
        public Guid Id { get; private set; }
        public Guid ClubId { get; private set; }
        public string Title { get; private set; }
        public string Content { get; private set; }
        public ReportStatus Status { get; private set; }
        public Guid CreatedBy { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? SubmittedAt { get; private set; }

        // EF Core Constructor
        private Report() { }

        public Report(Guid id, Guid clubId, string title, string content, Guid createdBy)
        {
            if (id == Guid.Empty)
                throw new InvalidDomainException("Report ID cannot be empty.");

            if (clubId == Guid.Empty)
                throw new InvalidDomainException("Club ID cannot be empty.");

            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidDomainException("Report Title cannot be empty.");

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidDomainException("Report Content cannot be empty.");

            if (createdBy == Guid.Empty)
                throw new InvalidDomainException("CreatedBy cannot be empty.");

            Id = id;
            ClubId = clubId;
            Title = title;
            Content = content;
            Status = ReportStatus.Draft;
            CreatedBy = createdBy;
            CreatedAt = DateTime.UtcNow;
        }

        public void Submit()
        {
            if (Status != ReportStatus.Draft)
            {
                throw new InvalidDomainException($"Cannot submit a report that is in {Status} status.");
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                throw new InvalidDomainException("Cannot submit a report with an empty Title.");
            }

            if (string.IsNullOrWhiteSpace(Content))
            {
                throw new InvalidDomainException("Cannot submit a report with empty Content.");
            }

            Status = ReportStatus.Submitted;
            SubmittedAt = DateTime.UtcNow;
        }

        public void Approve()
        {
            if (Status != ReportStatus.Submitted)
            {
                throw new InvalidDomainException("Only submitted reports can be approved.");
            }
            Status = ReportStatus.Approved;
        }

        public void Reject()
        {
            if (Status != ReportStatus.Submitted)
            {
                throw new InvalidDomainException("Only submitted reports can be rejected.");
            }
            Status = ReportStatus.Rejected;
        }
    }
}
