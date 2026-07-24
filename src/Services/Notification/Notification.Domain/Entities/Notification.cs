using System;
using Notification.Domain.Enums;
using Shared.Kernel.Domain;

namespace Notification.Domain.Entities
{
    public class NotificationEntity : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool IsRead { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? TargetUrl { get; set; }
        public DateTime? ReadAt { get; set; }
        public Guid? SourceEventId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
