namespace Buddy.Api.Models.Entities;

public sealed class ChildSessionLease
{
    public Guid ChildProfileId { get; set; } // PK
    public ChildProfile? ChildProfile { get; set; }

    public Guid ActiveSessionId { get; set; }
    public Guid? DeviceBindingId { get; set; }
    public string HeartbeatToken { get; set; } = string.Empty;
    public DateTime LeaseExpiresAtUtc { get; set; }
    public DateTime AcquiredAtUtc { get; set; } = DateTime.UtcNow;
}
