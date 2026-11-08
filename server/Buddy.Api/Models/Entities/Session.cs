namespace Buddy.Api.Models.Entities;

public sealed class Session
{
    public Guid Id { get; set; } = Guid.NewGuid(); // ApplicationSessionId
    public Guid FamilyId { get; set; }
    public Family? Family { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile? ChildProfile { get; set; }

    public Guid? DeviceBindingId { get; set; }
    public DeviceBinding? DeviceBinding { get; set; }

    public string? AzureSessionId { get; set; }
    public string? AzureCallId { get; set; }
    public string Mode { get; set; } = "child";
    public string Status { get; set; } = "Active"; // Active, Checkpointed, Finalized, Abandoned, Error

    public int DurationSeconds { get; set; }
    public int MemoryVersionAtStart { get; set; } = 1;
    public bool TranscriptConsentSnapshot { get; set; }

    /// <summary>
    /// Temporary structured transcript JSON. Encrypted or wiped after Outbox digestion (retention <= 24h).
    /// </summary>
    public string? StructuredTranscriptJson { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public DateTime? CheckpointedAtUtc { get; set; }

    public List<ToolInvocation> ToolInvocations { get; set; } = [];
    public List<BuddyActivity> Activities { get; set; } = [];
}
