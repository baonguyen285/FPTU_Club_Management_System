using System;
using System.Collections.Generic;
using Report.Domain.Enums;
using Shared.Kernel.Domain;
using Shared.Kernel.Exceptions;

namespace Report.Domain.Entities
{
    public class Report : BaseEntity
    {
        public string Title { get; private set; } = string.Empty;
        public string Content { get; private set; } = string.Empty;
        public ReportType Type { get; private set; }
        public ReportStatus Status { get; private set; }
        public Guid ClubId { get; private set; }
        public Guid CreatedBy { get; private set; }
        public Guid? ReviewedBy { get; private set; }
        public string? ReviewNote { get; private set; }
        
        public ICollection<ReportAttachment> Attachments { get; private set; } = new List<ReportAttachment>();

        // EF Core Constructor
        private Report() { }

        public Report(Guid clubId, string title, string content, ReportType type, Guid createdBy)
        {
            if (clubId == Guid.Empty)
                throw new InvalidDomainException("Club ID cannot be empty.");

            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidDomainException("Report Title cannot be empty.");

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidDomainException("Report Content cannot be empty.");

            if (createdBy == Guid.Empty)
                throw new InvalidDomainException("CreatedBy cannot be empty.");

            Id = Guid.NewGuid();
            ClubId = clubId;
            Title = title;
            Content = content;
            Type = type;
            Status = ReportStatus.Pending;
            CreatedBy = createdBy;
        }

        public void Update(string title, string content, ReportType type)
        {
            if (Status != ReportStatus.Pending)
                throw new InvalidDomainException("Only pending reports can be updated.");

            Title = title;
            Content = content;
            Type = type;
        }

        public void Approve(Guid reviewedBy, string? note = null)
        {
            if (Status != ReportStatus.Pending)
                throw new InvalidDomainException("Only pending reports can be approved.");

            if (reviewedBy == Guid.Empty)
                throw new InvalidDomainException("ReviewedBy cannot be empty.");

            Status = ReportStatus.Approved;
            ReviewedBy = reviewedBy;
            ReviewNote = note;
        }

        public void Reject(Guid reviewedBy, string reason)
        {
            if (Status != ReportStatus.Pending)
                throw new InvalidDomainException("Only pending reports can be rejected.");

            if (reviewedBy == Guid.Empty)
                throw new InvalidDomainException("ReviewedBy cannot be empty.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidDomainException("Reject reason cannot be empty.");

            Status = ReportStatus.Rejected;
            ReviewedBy = reviewedBy;
            ReviewNote = reason;
        }

        public void AddAttachment(string url, string fileName)
        {
            Attachments.Add(new ReportAttachment(url, fileName, Id));
        }
    }
}
