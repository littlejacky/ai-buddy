namespace Buddy.Api.Models.Entities;

public sealed class Family
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "我的家庭";
    public string Status { get; set; } = "Active";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Guardian> Guardians { get; set; } = [];
    public List<ChildProfile> Children { get; set; } = [];
    public List<DeviceBinding> Devices { get; set; } = [];
}
