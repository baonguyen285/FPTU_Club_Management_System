using Shared.Kernel.Domain;

namespace Notification.Domain.Entities;

public sealed class StreamProcessingFailure : BaseEntity
{
    public string StreamEntryId { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string LastErrorCode { get; set; } = string.Empty;
    public string LastErrorMessage { get; set; } = string.Empty;
    public DateTime LastFailedAtUtc { get; set; }
}
