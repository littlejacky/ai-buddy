using System.Text.Json;
using System.Text.Json.Serialization;
using Buddy.Api.Models;
using Microsoft.Extensions.Options;

namespace Buddy.Api.Services;

/// <summary>
/// Manages realtime session creation with the Azure OpenAI Realtime API.
/// Adheres strictly to the GA REST API schema for /openai/v1/realtime/client_secrets.
/// </summary>
public sealed class RealtimeSessionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureRealtimeOptions _options;
    private readonly PersonaService _personaService;
    private readonly ILogger<RealtimeSessionService> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public RealtimeSessionService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureRealtimeOptions> options,
        PersonaService personaService,
        ILogger<RealtimeSessionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _personaService = personaService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new realtime session and returns the ephemeral key for client-side WebRTC connection.
    /// Uses GA REST endpoint: POST /openai/v1/realtime/client_secrets (no api-version parameter).
    /// </summary>
    public async Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct = default)
    {
        var voice = ResolveVoice(request.Voice);
        var instructions = _personaService.GetInstructions(request.Mode);
        var rawExpires = request.ExpiresAfterSeconds ?? _options.DefaultExpiresAfterSeconds;
        // Azure supports 10..7200s, but the secret only needs to live long enough to start the call: cap at 600s.
        var expiresAfterSeconds = Math.Clamp(rawExpires, 10, 600);
        var isChild = request.Mode.Equals("child", StringComparison.OrdinalIgnoreCase);

        // Turn detection configuration: evaluate semantic_vad vs server_vad
        object turnDetectionConfig;
        var tdType = (request.TurnDetectionType ?? "semantic_vad").ToLowerInvariant();
        if (tdType == "server_vad")
        {
            turnDetectionConfig = new
            {
                type = "server_vad",
                threshold = 0.5,
                prefix_padding_ms = 300,
                silence_duration_ms = isChild ? 800 : 500,
                create_response = true
            };
        }
        else
        {
            turnDetectionConfig = new
            {
                type = "semantic_vad",
                eagerness = isChild ? "low" : "medium"
            };
        }

        // GA REST Reference: output_modalities accepts ["audio"] or ["text"].
        // Setting ["audio"] delivers synthesized audio via WebRTC media tracks AND text transcripts over DataChannel.
        object requestBody;

        if (request.MinimalConfig == true)
        {
            // Minimal GA request for Gate 0 probe verification
            requestBody = new
            {
                expires_after = new
                {
                    anchor = "created_at",
                    seconds = expiresAfterSeconds
                },
                session = new
                {
                    type = "realtime",
                    model = _options.DeploymentName
                }
            };
        }
        else
        {
            // Full GA configuration
            var inputConfig = new Dictionary<string, object>
            {
                ["turn_detection"] = turnDetectionConfig
            };

            // Only attach transcription when explicitly configured with a valid deployment name
            if (!string.IsNullOrWhiteSpace(_options.TranscriptionDeploymentName))
            {
                inputConfig["transcription"] = new { model = _options.TranscriptionDeploymentName };
            }

            requestBody = new
            {
                expires_after = new
                {
                    anchor = "created_at",
                    seconds = expiresAfterSeconds
                },
                session = new
                {
                    type = "realtime",
                    model = _options.DeploymentName, // Azure Deployment Name
                    instructions,
                    output_modalities = new[] { "audio" },
                    audio = new
                    {
                        input = inputConfig,
                        output = new
                        {
                            voice
                        }
                    }
                }
            };
        }

        var baseEndpoint = _options.Endpoint?.TrimEnd('/') ?? string.Empty;
        if (Uri.TryCreate(baseEndpoint, UriKind.Absolute, out var parsedUri))
        {
            baseEndpoint = $"{parsedUri.Scheme}://{parsedUri.Authority}";
        }

        var url = $"{baseEndpoint}/openai/v1/realtime/client_secrets";

        using var httpClient = _httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("api-key", _options.ApiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, s_jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        _logger.LogInformation(
            "Requesting ephemeral client secret for mode '{Mode}' with voice '{Voice}', turn detection '{TurnDetection}', expiresAfter {ExpiresAfter}s",
            request.Mode, voice, tdType, expiresAfterSeconds);

        using var response = await httpClient.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Azure Realtime API returned HTTP {StatusCode}: {Error}", (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var secretResponse = JsonSerializer.Deserialize<AzureClientSecretResponse>(responseJson, s_jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Azure Realtime client_secrets response.");

        var ephemeralKey = secretResponse.Value;
        if (string.IsNullOrWhiteSpace(ephemeralKey))
        {
            throw new InvalidOperationException("Azure Realtime API returned an empty ephemeral key (checked root 'value').");
        }

        var expiresAt = secretResponse.ExpiresAt ?? (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresAfterSeconds);
        var azureSessionId = secretResponse.Session?.Id; // Azure's actual session ID, null if not returned
        var businessSessionId = $"biz_sess_{Guid.NewGuid():N}"; // Local application business session identifier

        _logger.LogInformation("Ephemeral secret minted successfully. BusinessSessionId: {BusinessSessionId}, AzureSessionId: {AzureSessionId}, ExpiresAt: {ExpiresAt}",
            businessSessionId, azureSessionId ?? "<unassigned>", expiresAt);

        return new CreateSessionResponse
        {
            EphemeralKey = ephemeralKey,
            ExpiresAt = expiresAt,
            BusinessSessionId = businessSessionId,
            AzureSessionId = azureSessionId,
            Endpoint = GetEndpoint(),
            MaxSessionSeconds = isChild ? _options.ChildMaxSessionSeconds : _options.AdultMaxSessionSeconds
        };
    }

    /// <summary>
    /// Returns the configured Azure OpenAI resource endpoint.
    /// </summary>
    public string GetEndpoint() => _options.Endpoint.TrimEnd('/');

    private string ResolveVoice(string? requestedVoice)
    {
        if (string.IsNullOrWhiteSpace(requestedVoice))
        {
            return _options.DefaultVoice;
        }

        var matched = AzureRealtimeOptions.SupportedVoices
            .FirstOrDefault(v => v.Equals(requestedVoice, StringComparison.OrdinalIgnoreCase));

        if (matched is null)
        {
            _logger.LogWarning("Requested voice '{RequestedVoice}' is not in official supported list. Falling back to default '{DefaultVoice}'",
                requestedVoice, _options.DefaultVoice);
            return _options.DefaultVoice;
        }

        return matched;
    }
}
