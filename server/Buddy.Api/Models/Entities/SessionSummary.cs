namespace Buddy.Api.Models.Entities;

public sealed class SessionSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile? ChildProfile { get; set; }

    public string TopicsJson { get; set; } = "[]";
    public string CuriosityHighlight { get; set; } = string.Empty;
    public string FamilyActivitySuggestion { get; set; } = string.Empty;
    public string NextTopicsJson { get; set; } = "[]";
    public string? ExpressedFeelings { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
