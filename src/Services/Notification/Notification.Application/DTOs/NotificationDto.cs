using System;

namespace Notification.Application.DTOs
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? TargetUrl { get; set; }
        public Guid? SourceEventId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
