using System.Text.Json;
using Buddy.Api.Application.FamilyManagement;
using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Application.Safety;

public sealed record SafetyEventDto(
    Guid Id,
    Guid? SessionId,
    string Severity,
    string Category,
    string ContextEvidence,
    string? ResolutionNotes,
    bool IsAcknowledgedByGuardian,
    DateTime DetectedAtUtc
);

public sealed class SafetyAuditService
{
    private readonly BuddyDbContext _db;
    private readonly ConsentService _consentService;
    private readonly ILogger<SafetyAuditService> _logger;

    public SafetyAuditService(
        BuddyDbContext db,
        ConsentService consentService,
        ILogger<SafetyAuditService> logger)
    {
        _db = db;
        _consentService = consentService;
        _logger = logger;
    }

    public async Task<List<SafetyEventDto>> GetSafetyEventsAsync(Guid familyId, Guid childProfileId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return [];

        var events = await _db.SafetyEvents
            .Where(s => s.ChildProfileId == childProfileId)
            .OrderByDescending(s => s.DetectedAtUtc)
            .ToListAsync(ct);

        return events.Select(s => new SafetyEventDto(
            s.Id,
            s.SessionId,
            s.Severity,
            s.Category,
            s.ContextEvidence,
            s.ResolutionNotes,
            s.IsAcknowledgedByGuardian,
            s.DetectedAtUtc
        )).ToList();
    }

    public async Task<bool> AcknowledgeSafetyEventAsync(Guid familyId, Guid childProfileId, Guid eventId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return false;

        var ev = await _db.SafetyEvents
            .FirstOrDefaultAsync(s => s.ChildProfileId == childProfileId && s.Id == eventId, ct);
        if (ev is null) return false;

        ev.IsAcknowledgedByGuardian = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SafetyEvent?> ScanTranscriptForSafetyAsync(
        Guid sessionId,
        Guid childProfileId,
        string structuredTranscriptJson,
        CancellationToken ct = default)
    {
        var hasSafetyConsent = await _consentService.IsConsentGrantedAsync(childProfileId, "ChildSafetyAudit", ct);
        if (!hasSafetyConsent) return null;

        List<JsonElement>? turns = null;
        try
        {
            turns = JsonSerializer.Deserialize<List<JsonElement>>(structuredTranscriptJson);
        }
        catch { return null; }

        if (turns is null || turns.Count == 0) return null;

        var fullUserSpeech = new List<string>();
        foreach (var t in turns)
        {
            if (t.TryGetProperty("role", out var r) && r.GetString() == "user" &&
                t.TryGetProperty("text", out var txt) && !string.IsNullOrWhiteSpace(txt.GetString()))
            {
                fullUserSpeech.Add(txt.GetString()!.Trim());
            }
        }

        var text = string.Join(" ", fullUserSpeech);

        // S2 keyword patterns (Urgent Danger / Self-Harm)
        if (text.Contains("不想活") || text.Contains("自杀") || text.Contains("割腕") || text.Contains("跳楼"))
        {
            var s2Event = new SafetyEvent
            {
                SessionId = sessionId,
                ChildProfileId = childProfileId,
                Severity = "S2",
                Category = "HarmRisk",
                ContextEvidence = "检测到可能涉及极端自我伤害的倾向言语。",
                ResolutionNotes = "AI 已引导寻找信任成年人或求助热线。建议监护人立即给予线下关注和心理关怀。",
                DetectedAtUtc = DateTime.UtcNow
            };
            _db.SafetyEvents.Add(s2Event);
            await _db.SaveChangesAsync(ct);
            return s2Event;
        }

        // S1 keyword patterns (Bullying / Distress)
        if (text.Contains("打我") || text.Contains("被欺负") || text.Contains("霸凌") || text.Contains("好害怕") || text.Contains("救命"))
        {
            var s1Event = new SafetyEvent
            {
                SessionId = sessionId,
                ChildProfileId = childProfileId,
                Severity = "S1",
                Category = "Bullying",
                ContextEvidence = "小朋友在交谈中提及受到欺负或严重害怕情绪。",
                ResolutionNotes = "AI 已温柔安慰并鼓励告诉爸爸妈妈或老师。建议家长温和沟通核实学校情况。",
                DetectedAtUtc = DateTime.UtcNow
            };
            _db.SafetyEvents.Add(s1Event);
            await _db.SaveChangesAsync(ct);
            return s1Event;
        }

        return null;
    }
}
