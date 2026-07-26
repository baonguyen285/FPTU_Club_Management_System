using System;
using System.Collections.Generic;

namespace Report.Application.DTOs
{
    public class ReportDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid ClubId { get; set; }
        public Guid? SemesterId { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? ReviewedBy { get; set; }
        public string? ReviewNote { get; set; }
        public int RevisionNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<ReportAttachmentDto> Attachments { get; set; } = new List<ReportAttachmentDto>();
    }

    public sealed record ReportRevisionHistoryDto(
        Guid Id,
        Guid ReportId,
        int RevisionNumber,
        string PreviousStatus,
        string NewStatus,
        string? Feedback,
        Guid ChangedBy,
        DateTime ChangedAt);

    public class ReportAttachmentDto
    {
        public Guid Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
