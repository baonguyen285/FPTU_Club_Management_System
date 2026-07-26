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
        public Guid? SemesterId { get; private set; }
        public Guid CreatedBy { get; private set; }
        public Guid? ReviewedBy { get; private set; }
        public string? ReviewNote { get; private set; }
        public int RevisionNumber { get; private set; } = 1;
        
        public ICollection<ReportAttachment> Attachments { get; private set; } = new List<ReportAttachment>();

        // EF Core Constructor
        private Report() { }

        public Report(Guid clubId, string title, string content, ReportType type, Guid createdBy)
            : this(clubId, null, title, content, type, createdBy, ReportStatus.PendingApproval)
        {
        }

        public Report(Guid clubId, Guid semesterId, string title, string content, ReportType type, Guid createdBy)
            : this(clubId, semesterId, title, content, type, createdBy, ReportStatus.Draft)
        {
        }

        private Report(Guid clubId, Guid? semesterId, string title, string content, ReportType type, Guid createdBy, ReportStatus initialStatus)
        {
            if (clubId == Guid.Empty)
                throw new InvalidDomainException("Club ID cannot be empty.");

            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidDomainException("Report Title cannot be empty.");

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidDomainException("Report Content cannot be empty.");

            if (createdBy == Guid.Empty)
                throw new InvalidDomainException("CreatedBy cannot be empty.");
            if (semesterId.HasValue && semesterId.Value == Guid.Empty)
                throw new InvalidDomainException("Semester ID cannot be empty.");

            Id = Guid.NewGuid();
            ClubId = clubId;
            SemesterId = semesterId;
            Title = title;
            Content = content;
            Type = type;
            Status = initialStatus;
            CreatedBy = createdBy;
        }

        public void Update(string title, string content, ReportType type)
        {
            if (Status is not ReportStatus.Draft and not ReportStatus.RequestRevision)
                throw new ConflictException("Only draft or revision-requested reports can be updated.");

            Title = title;
            Content = content;
            Type = type;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Submit()
        {
            if (Status is not ReportStatus.Draft and not ReportStatus.RequestRevision)
                throw new ConflictException("Only draft or revision-requested reports can be submitted.");

            if (Status == ReportStatus.RequestRevision)
                RevisionNumber++;

            Status = ReportStatus.PendingApproval;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Approve(Guid reviewedBy, string? note = null)
        {
            if (Status != ReportStatus.PendingApproval)
                throw new ConflictException("Only pending reports can be approved.");

            if (reviewedBy == Guid.Empty)
                throw new InvalidDomainException("ReviewedBy cannot be empty.");

            Status = ReportStatus.Approved;
            ReviewedBy = reviewedBy;
            ReviewNote = note;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Reject(Guid reviewedBy, string reason)
        {
            if (Status != ReportStatus.PendingApproval)
                throw new ConflictException("Only pending reports can be rejected.");

            if (reviewedBy == Guid.Empty)
                throw new InvalidDomainException("ReviewedBy cannot be empty.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidDomainException("Reject reason cannot be empty.");

            Status = ReportStatus.Rejected;
            ReviewedBy = reviewedBy;
            ReviewNote = reason;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RequestRevision(Guid reviewedBy, string feedback)
        {
            if (Status != ReportStatus.PendingApproval)
                throw new ConflictException("Only pending reports can be returned for revision.");
            if (reviewedBy == Guid.Empty)
                throw new InvalidDomainException("ReviewedBy cannot be empty.");
            if (string.IsNullOrWhiteSpace(feedback))
                throw new InvalidDomainException("Revision feedback cannot be empty.");

            Status = ReportStatus.RequestRevision;
            ReviewedBy = reviewedBy;
            ReviewNote = feedback.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddAttachment(string url, string fileName)
        {
            Attachments.Add(new ReportAttachment(url, fileName, Id));
        }
    }
}
