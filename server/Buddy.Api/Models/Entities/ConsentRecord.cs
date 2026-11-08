namespace Buddy.Api.Models.Entities;

public sealed class ConsentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GuardianId { get; set; }
    public Guardian? Guardian { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile? ChildProfile { get; set; }

    public string ConsentType { get; set; } = string.Empty; // VoiceService, CrossSessionMemory, ParentDiscoveryDigest, TranscriptProcessing, ChildSafetyAudit, ApproximateLocation, PublicWebSearch
    public string Version { get; set; } = "1.0";
    public bool IsGranted { get; set; }
    public DateTime? GrantedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
