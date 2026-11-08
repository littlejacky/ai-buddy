namespace Buddy.Api.Models.Entities;

public sealed class ToolInvocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public string ToolName { get; set; } = string.Empty;
    public string InputJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public bool IsSuccess { get; set; } = true;
    public int DurationMs { get; set; }
    public DateTime InvokedAtUtc { get; set; } = DateTime.UtcNow;
}
