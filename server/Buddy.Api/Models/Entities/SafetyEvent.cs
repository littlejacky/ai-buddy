namespace Buddy.Api.Models.Entities;

public sealed class SafetyEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildProfileId { get; set; }
    public ChildProfile? ChildProfile { get; set; }

    public Guid? SessionId { get; set; }
    public string Severity { get; set; } = "S0"; // S0, S1, S2
    public string Category { get; set; } = "Other"; // Distress, Bullying, HarmRisk, Other
    public string ContextEvidence { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }
    public bool IsAcknowledgedByGuardian { get; set; }
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}
