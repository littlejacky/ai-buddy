namespace Buddy.Api.Models.Entities;

public sealed class BuddyActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public string Kind { get; set; } = "knowledge"; // knowledge, quiz, flashcard, story
    public string Title { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "1.0";
    public string PayloadJson { get; set; } = "{}";
    public string? UserResponseJson { get; set; }
    public DateTime SpawnedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
}
