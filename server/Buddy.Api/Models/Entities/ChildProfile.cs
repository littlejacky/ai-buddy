namespace Buddy.Api.Models.Entities;

public sealed class ChildProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public Family? Family { get; set; }

    public string Nickname { get; set; } = "小朋友";
    public string AgeBand { get; set; } = "7-8"; // 4-6, 7-8, 9-10
    public string PreferredVoice { get; set; } = "marin"; // marin, cedar, alloy
    public string LanguagePreference { get; set; } = "zh-CN";
    public int CurrentMemoryVersion { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Session> Sessions { get; set; } = [];
    public List<MemoryFact> MemoryFacts { get; set; } = [];
    public List<SessionSummary> Summaries { get; set; } = [];
    public List<SafetyEvent> SafetyEvents { get; set; } = [];
}
