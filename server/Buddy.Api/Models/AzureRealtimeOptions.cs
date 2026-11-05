namespace Buddy.Api.Models;

/// <summary>
/// Configuration options for Azure OpenAI Realtime API.
/// Bound from the "AzureRealtime" configuration section.
/// </summary>
public sealed class AzureRealtimeOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AzureRealtime";

    /// <summary>Azure OpenAI resource endpoint (e.g. https://my-resource.openai.azure.com).</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>API key for authenticating with Azure OpenAI (for local dev; Managed Identity in production).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Azure OpenAI deployment name for the realtime model (NOT model ID).</summary>
    public string DeploymentName { get; set; } = "";

    /// <summary>Model version (Official current directory version: "2026-07-07").</summary>
    public string ModelVersion { get; set; } = "2026-07-07";

    /// <summary>Default voice for the realtime session. Official: alloy, ash, ballad, coral, cedar, echo, marin, sage, shimmer, verse.</summary>
    public string DefaultVoice { get; set; } = "marin";

    /// <summary>Default lifetime of ephemeral client secrets in seconds (GA standard default: 600).</summary>
    public int DefaultExpiresAfterSeconds { get; set; } = 600;

    /// <summary>Max session length in seconds for child mode (0 = unlimited). Enforced by the client, policy owned by the server.</summary>
    public int ChildMaxSessionSeconds { get; set; } = 900;

    /// <summary>Max session length in seconds for adult mode (0 = unlimited).</summary>
    public int AdultMaxSessionSeconds { get; set; } = 0;

    /// <summary>Optional Azure Whisper/Transcription deployment name (null if disabled).</summary>
    public string? TranscriptionDeploymentName { get; set; }

    /// <summary>Official currently supported voice list.</summary>
    public static readonly string[] SupportedVoices =
    [
        "alloy", "ash", "ballad", "coral", "cedar", "echo", "marin", "sage", "shimmer", "verse"
    ];
}
