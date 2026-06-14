using System;

namespace Notification.Application.DTOs
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public int Type { get; set; }
        public bool IsRead { get; set; }
        public Guid? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
