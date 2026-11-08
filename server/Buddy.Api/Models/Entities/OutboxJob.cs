namespace Buddy.Api.Models.Entities;

public sealed class OutboxJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JobType { get; set; } = string.Empty; // ProcessSessionFinalize, GenerateDigest, ExtractMemory, AuditSafety
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? LockInstanceId { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
