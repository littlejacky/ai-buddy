namespace Buddy.Api.Models.Entities;

public sealed class DeviceBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public Family? Family { get; set; }

    public Guid? BoundChildProfileId { get; set; }
    public ChildProfile? BoundChildProfile { get; set; }

    public string DeviceIdentifier { get; set; } = string.Empty; // Persistent client device fingerprint/UUID
    public string DeviceName { get; set; } = "家庭设备";
    public string Platform { get; set; } = "Web"; // iOS, Android, Desktop
    public bool IsAuthorized { get; set; } = true;
    public DateTime LastActiveAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
