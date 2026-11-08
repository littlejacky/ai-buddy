using System.Text.Json;
using Buddy.Api.Application.FamilyManagement;
using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Application.SessionManagement;

public sealed record TurnRecord(
    string Id,
    string Role, // user, assistant, system
    string Text,
    long At,
    bool? Interrupted
);

public sealed record CheckpointRequest(
    List<TurnRecord> Turns
);

public sealed record FinalizeSessionRequest(
    int DurationSeconds,
    List<TurnRecord> FinalTurns
);

public sealed record FinalizeSessionResponse(
    bool Success,
    string Status,
    Guid? OutboxJobId
);

public sealed class SessionManager
{
    private readonly BuddyDbContext _db;
    private readonly ConsentService _consentService;
    private readonly SessionLeaseService _leaseService;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(
        BuddyDbContext db,
        ConsentService consentService,
        SessionLeaseService leaseService,
        ILogger<SessionManager> logger)
    {
        _db = db;
        _consentService = consentService;
        _leaseService = leaseService;
        _logger = logger;
    }

    public async Task<Session> StartSessionAsync(
        Guid familyId,
        Guid childProfileId,
        Guid? deviceBindingId,
        string mode,
        string? azureSessionId,
        CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct)
            ?? throw new KeyNotFoundException($"Child profile {childProfileId} not found in family {familyId}");

        var hasTranscriptConsent = await _consentService.IsConsentGrantedAsync(childProfileId, "TranscriptProcessing", ct);

        var session = new Session
        {
            FamilyId = familyId,
            ChildProfileId = childProfileId,
            DeviceBindingId = deviceBindingId,
            AzureSessionId = azureSessionId,
            Mode = mode,
            Status = "Active",
            MemoryVersionAtStart = child.CurrentMemoryVersion,
            TranscriptConsentSnapshot = hasTranscriptConsent,
            StartedAtUtc = DateTime.UtcNow
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<bool> CheckpointAsync(
        Guid sessionId,
        Guid familyId,
        CheckpointRequest request,
        CancellationToken ct = default)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.FamilyId == familyId, ct);

        if (session is null || session.Status == "Finalized")
        {
            return false;
        }

        session.CheckpointedAtUtc = DateTime.UtcNow;

        if (session.TranscriptConsentSnapshot && request.Turns.Count > 0)
        {
            // Clean up and structure turns without arbitrary log pollution
            session.StructuredTranscriptJson = JsonSerializer.Serialize(request.Turns);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<FinalizeSessionResponse> FinalizeSessionAsync(
        Guid sessionId,
        Guid familyId,
        FinalizeSessionRequest request,
        CancellationToken ct = default)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.FamilyId == familyId, ct);

        if (session is null)
        {
            return new FinalizeSessionResponse(false, "NotFound", null);
        }

        // Idempotency: If already finalized, return existing state without duplicate OutboxJobs
        if (session.Status == "Finalized")
        {
            _logger.LogInformation("Session {SessionId} is already finalized. Returning idempotent success.", sessionId);
            return new FinalizeSessionResponse(true, "Finalized", null);
        }

        session.Status = "Finalized";
        session.DurationSeconds = request.DurationSeconds;
        session.EndedAtUtc = DateTime.UtcNow;

        if (session.TranscriptConsentSnapshot && request.FinalTurns.Count > 0)
        {
            session.StructuredTranscriptJson = JsonSerializer.Serialize(request.FinalTurns);
        }

        // Release child session lease so new sessions can begin immediately
        await _leaseService.ReleaseLeaseAsync(session.ChildProfileId, session.Id, ct);

        // Transactional Outbox Job generation
        Guid? outboxJobId = null;
        if (session.TranscriptConsentSnapshot && !string.IsNullOrWhiteSpace(session.StructuredTranscriptJson))
        {
            var jobPayload = JsonSerializer.Serialize(new
            {
                SessionId = session.Id,
                ChildProfileId = session.ChildProfileId,
                FamilyId = session.FamilyId
            });

            var job = new OutboxJob
            {
                JobType = "ProcessSessionFinalize",
                PayloadJson = jobPayload,
                Status = "Pending"
            };

            _db.OutboxJobs.Add(job);
            outboxJobId = job.Id;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Session {SessionId} finalized successfully. OutboxJobId: {JobId}",
            sessionId, outboxJobId?.ToString() ?? "<none>");

        return new FinalizeSessionResponse(true, "Finalized", outboxJobId);
    }

    public async Task<Session?> GetSessionAsync(Guid sessionId, Guid familyId, CancellationToken ct = default)
    {
        return await _db.Sessions
            .Include(s => s.Activities)
            .Include(s => s.ToolInvocations)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.FamilyId == familyId, ct);
    }
}
