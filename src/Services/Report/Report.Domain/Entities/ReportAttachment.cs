using System;
using Shared.Kernel.Domain;
using Shared.Kernel.Exceptions;

namespace Report.Domain.Entities
{
    public class ReportAttachment : BaseEntity
    {
        public string Url { get; private set; } = string.Empty;
        public string FileName { get; private set; } = string.Empty;
        public Guid ReportId { get; private set; }
        public Report Report { get; private set; } = null!;

        // EF Core Constructor
        private ReportAttachment() { }

        public ReportAttachment(string url, string fileName, Guid reportId)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidDomainException("Attachment URL cannot be empty.");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidDomainException("Attachment FileName cannot be empty.");

            if (reportId == Guid.Empty)
                throw new InvalidDomainException("Report ID cannot be empty.");

            Id = Guid.NewGuid();
            Url = url;
            FileName = fileName;
            ReportId = reportId;
        }
    }
}
