using System.Text.Json.Serialization;

namespace Buddy.Api.Models;

/// <summary>
/// Request payload for creating a new realtime session.
/// </summary>
public sealed class CreateSessionRequest
{
    /// <summary>Persona mode: "adult" or "child".</summary>
    [JsonPropertyName("mode")]
    public required string Mode { get; set; }

    /// <summary>
    /// Optional voice override.
    /// Official supported voices: alloy, ash, ballad, coral, cedar, echo, marin, sage, shimmer, verse.
    /// </summary>
    [JsonPropertyName("voice")]
    public string? Voice { get; set; }

    /// <summary>
    /// Turn detection mode: "server_vad" or "semantic_vad".
    /// </summary>
    [JsonPropertyName("turnDetectionType")]
    public string? TurnDetectionType { get; set; }

    /// <summary>
    /// Optional token lifetime in seconds (default 600).
    /// </summary>
    [JsonPropertyName("expiresAfterSeconds")]
    public int? ExpiresAfterSeconds { get; set; }

    /// <summary>
    /// If true, sends minimal GA request body without extra parameters for Gate 0 probe testing.
    /// </summary>
    [JsonPropertyName("minimalConfig")]
    public bool? MinimalConfig { get; set; }
}

/// <summary>
/// Response returned to the client after session creation.
/// Explicitly separates Local Business Session ID from Azure Realtime Session ID.
/// </summary>
public sealed class CreateSessionResponse
{
    /// <summary>Ephemeral client secret (e.g. "ek_...") for WebRTC SDP calls.</summary>
    [JsonPropertyName("ephemeralKey")]
    public required string EphemeralKey { get; set; }

    /// <summary>Unix timestamp (in seconds) when the ephemeral secret expires.</summary>
    [JsonPropertyName("expiresAt")]
    public required long ExpiresAt { get; set; }

    /// <summary>Local application business session identifier.</summary>
    [JsonPropertyName("businessSessionId")]
    public required string BusinessSessionId { get; set; }

    /// <summary>
    /// Azure OpenAI Realtime Session ID returned from Azure (/client_secrets -> session.id).
    /// Null if not populated by Azure at secret minting time.
    /// </summary>
    [JsonPropertyName("azureSessionId")]
    public string? AzureSessionId { get; set; }

    /// <summary>Azure OpenAI resource endpoint for WebRTC calls.</summary>
    [JsonPropertyName("endpoint")]
    public required string Endpoint { get; set; }

    /// <summary>Server-owned session length cap in seconds (0 = unlimited).</summary>
    [JsonPropertyName("maxSessionSeconds")]
    public int MaxSessionSeconds { get; set; }
}

/// <summary>
/// DTO representing Azure OpenAI /openai/v1/realtime/client_secrets response.
/// Per GA REST Reference: 'value', 'expires_at', and 'session' are top-level fields.
/// </summary>
public sealed class AzureClientSecretResponse
{
    /// <summary>Ephemeral token string (top-level in GA schema).</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Unix timestamp in seconds when secret expires (top-level in GA schema).</summary>
    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; }

    /// <summary>Session configuration and metadata returned with the secret.</summary>
    [JsonPropertyName("session")]
    public SessionDetails? Session { get; set; }
}

/// <summary>
/// Session metadata from Azure response.
/// </summary>
public sealed class SessionDetails
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }
}
