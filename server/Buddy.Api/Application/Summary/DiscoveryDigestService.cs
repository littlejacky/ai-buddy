using System.Text.Json;
using Buddy.Api.Application.FamilyManagement;
using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Application.Summary;

public sealed record DigestDto(
    Guid Id,
    Guid SessionId,
    List<string> Topics,
    string CuriosityHighlight,
    string FamilyActivitySuggestion,
    List<string> NextTopics,
    string? ExpressedFeelings,
    DateTime GeneratedAtUtc
);

public sealed class DiscoveryDigestService
{
    private readonly BuddyDbContext _db;
    private readonly ConsentService _consentService;
    private readonly ILogger<DiscoveryDigestService> _logger;

    public DiscoveryDigestService(
        BuddyDbContext db,
        ConsentService consentService,
        ILogger<DiscoveryDigestService> logger)
    {
        _db = db;
        _consentService = consentService;
        _logger = logger;
    }

    public async Task<List<DigestDto>> GetChildDigestsAsync(Guid familyId, Guid childProfileId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return [];

        var summaries = await _db.SessionSummaries
            .Where(s => s.ChildProfileId == childProfileId)
            .OrderByDescending(s => s.GeneratedAtUtc)
            .Take(30)
            .ToListAsync(ct);

        return summaries.Select(s => new DigestDto(
            s.Id,
            s.SessionId,
            DeserializeList(s.TopicsJson),
            s.CuriosityHighlight,
            s.FamilyActivitySuggestion,
            DeserializeList(s.NextTopicsJson),
            s.ExpressedFeelings,
            s.GeneratedAtUtc
        )).ToList();
    }

    public async Task<bool> DeleteDigestAsync(Guid familyId, Guid childProfileId, Guid digestId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return false;

        var summary = await _db.SessionSummaries
            .FirstOrDefaultAsync(s => s.ChildProfileId == childProfileId && s.Id == digestId, ct);
        if (summary is null) return false;

        _db.SessionSummaries.Remove(summary);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SessionSummary?> GenerateDigestFromTranscriptAsync(
        Guid sessionId,
        Guid childProfileId,
        string structuredTranscriptJson,
        CancellationToken ct = default)
    {
        var hasDigestConsent = await _consentService.IsConsentGrantedAsync(childProfileId, "ParentDiscoveryDigest", ct);
        if (!hasDigestConsent)
        {
            _logger.LogInformation("Skipping digest generation for session {SessionId}: ParentDiscoveryDigest consent not granted.", sessionId);
            return null;
        }

        // Parse transcript turns
        List<JsonElement>? turns = null;
        try
        {
            turns = JsonSerializer.Deserialize<List<JsonElement>>(structuredTranscriptJson);
        }
        catch { return null; }

        if (turns is null || turns.Count == 0) return null;

        // Extract user inputs to ground topics
        var userPhrases = new List<string>();
        foreach (var t in turns)
        {
            if (t.TryGetProperty("role", out var r) && r.GetString() == "user" &&
                t.TryGetProperty("text", out var txt) && !string.IsNullOrWhiteSpace(txt.GetString()))
            {
                userPhrases.Add(txt.GetString()!.Trim());
            }
        }

        if (userPhrases.Count == 0) return null;

        // Structured digest generation (deterministic heuristic / rule-based for safety & repeatability)
        var topics = ExtractTopics(userPhrases);
        var curiosity = $"小朋友今天主动探讨了关于【{string.Join("、", topics)}】的奇思妙想，充满求知欲。";
        var familyActivity = $"晚饭或周末可以和孩子一起翻阅相关的图画书，或者动手做个小实验继续探索【{topics.FirstOrDefault() ?? "大自然"}】。";
        var nextTopics = new List<string> { $"{topics.FirstOrDefault() ?? "科学探索"}的秘密" };

        var summary = new SessionSummary
        {
            SessionId = sessionId,
            ChildProfileId = childProfileId,
            TopicsJson = JsonSerializer.Serialize(topics),
            CuriosityHighlight = curiosity,
            FamilyActivitySuggestion = familyActivity,
            NextTopicsJson = JsonSerializer.Serialize(nextTopics),
            ExpressedFeelings = "探索过程专注且愉快",
            GeneratedAtUtc = DateTime.UtcNow
        };

        _db.SessionSummaries.Add(summary);
        await _db.SaveChangesAsync(ct);
        return summary;
    }

    private static List<string> ExtractTopics(List<string> phrases)
    {
        var topics = new HashSet<string>();
        var fullText = string.Join(" ", phrases);

        // Child topic heuristics
        if (fullText.Contains("恐龙") || fullText.Contains("霸王龙") || fullText.Contains("三角龙")) topics.Add("恐龙探秘");
        if (fullText.Contains("鲸鱼") || fullText.Contains("海洋") || fullText.Contains("鲨鱼") || fullText.Contains("大海")) topics.Add("海洋生物");
        if (fullText.Contains("宇宙") || fullText.Contains("月亮") || fullText.Contains("太阳") || fullText.Contains("星星")) topics.Add("太空天文");
        if (fullText.Contains("汽车") || fullText.Contains("火车") || fullText.Contains("飞机")) topics.Add("机械交通");
        if (fullText.Contains("动物") || fullText.Contains("小狗") || fullText.Contains("小猫")) topics.Add("自然动物");

        if (topics.Count == 0)
        {
            topics.Add("趣味生活对话");
        }

        return topics.Take(3).ToList();
    }

    private static List<string> DeserializeList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
