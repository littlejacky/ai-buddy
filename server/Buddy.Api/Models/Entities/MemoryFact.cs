namespace Buddy.Api.Models.Entities;

public sealed class MemoryFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildProfileId { get; set; }
    public ChildProfile? ChildProfile { get; set; }

    public string FactType { get; set; } = "Interest"; // Interest, Preference, Habit, OpenQuestion, FriendOrFamily
    public string Content { get; set; } = string.Empty;
    public Guid? SourceSessionId { get; set; }
    public string SourceType { get; set; } = "child_explicit"; // child_explicit, guardian_provided, model_inferred
    public double Confidence { get; set; } = 1.0;
    public int MemoryVersion { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastConfirmedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
