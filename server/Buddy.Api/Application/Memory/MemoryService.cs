using System.Text;
using System.Text.Json;
using Buddy.Api.Application.FamilyManagement;
using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Application.Memory;

public sealed record MemoryFactDto(
    Guid Id,
    string FactType,
    string Content,
    string SourceType,
    double Confidence,
    int MemoryVersion,
    DateTime CreatedAtUtc
);

public sealed record CreateMemoryRequest(
    string FactType,
    string Content,
    string? SourceType
);

public sealed class MemoryService
{
    private readonly BuddyDbContext _db;
    private readonly ConsentService _consentService;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        BuddyDbContext db,
        ConsentService consentService,
        ILogger<MemoryService> logger)
    {
        _db = db;
        _consentService = consentService;
        _logger = logger;
    }

    public async Task<string> BuildMemoryInstructionBlockAsync(Guid familyId, Guid childProfileId, CancellationToken ct = default)
    {
        var hasMemoryConsent = await _consentService.IsConsentGrantedAsync(childProfileId, "CrossSessionMemory", ct);
        if (!hasMemoryConsent)
        {
            return string.Empty;
        }

        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return string.Empty;

        // Fetch active L1 MemoryFacts
        var facts = await _db.MemoryFacts
            .Where(m => m.ChildProfileId == childProfileId && m.IsActive)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);

        // Fetch latest L2 SessionSummary
        var latestSummary = await _db.SessionSummaries
            .Where(s => s.ChildProfileId == childProfileId)
            .OrderByDescending(s => s.GeneratedAtUtc)
            .FirstOrDefaultAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("【关于这个小朋友的长期伙伴记忆（仅在自然相关时提起，不要刻意罗列）】");
        sb.AppendLine($"- 小朋友昵称：{child.Nickname}（{child.AgeBand}岁）");

        if (facts.Count > 0)
        {
            sb.AppendLine("- 已知兴趣与偏好：");
            foreach (var f in facts)
            {
                sb.AppendLine($"  * {f.Content}（{f.FactType}）");
            }
        }

        if (latestSummary is not null)
        {
            sb.AppendLine($"- 上次聊天重点：{latestSummary.CuriosityHighlight}");
            if (!string.IsNullOrWhiteSpace(latestSummary.NextTopicsJson) && latestSummary.NextTopicsJson != "[]")
            {
                try
                {
                    var nextTopics = JsonSerializer.Deserialize<List<string>>(latestSummary.NextTopicsJson);
                    if (nextTopics?.Count > 0)
                    {
                        sb.AppendLine($"- 下次可延续的话题：{string.Join("、", nextTopics)}");
                    }
                }
                catch { /* ignore json parse */ }
            }
        }

        sb.AppendLine("""
            【记忆运用规则】
            1. 当小朋友主动提到相关事物时，自然接话（比如“上次你提到了……”）；绝不要一开场就背诵这些记忆。
            2. 如果小朋友纠正你（比如“我现在不喜欢恐龙了”），立刻顺应并接受纠正，不要辩解。
            3. 不把猜测当成事实。
            """);

        return sb.ToString().Trim();
    }

    public async Task<List<MemoryFactDto>> GetChildMemoriesAsync(Guid familyId, Guid childProfileId, CancellationToken ct = default)
    {
        // Enforce family tenant boundary
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return [];

        var facts = await _db.MemoryFacts
            .Where(m => m.ChildProfileId == childProfileId && m.IsActive)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        return facts.Select(f => new MemoryFactDto(
            f.Id,
            f.FactType,
            f.Content,
            f.SourceType,
            f.Confidence,
            f.MemoryVersion,
            f.CreatedAtUtc
        )).ToList();
    }

    public async Task<MemoryFact?> AddMemoryFactAsync(
        Guid familyId,
        Guid childProfileId,
        CreateMemoryRequest request,
        CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return null;

        // Deduplication: check if exact same content already exists
        var existing = await _db.MemoryFacts
            .FirstOrDefaultAsync(m => m.ChildProfileId == childProfileId && m.IsActive && m.Content == request.Content.Trim(), ct);

        if (existing is not null)
        {
            existing.LastConfirmedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        child.CurrentMemoryVersion++;
        child.UpdatedAtUtc = DateTime.UtcNow;

        var fact = new MemoryFact
        {
            ChildProfileId = childProfileId,
            FactType = request.FactType,
            Content = request.Content.Trim(),
            SourceType = request.SourceType ?? "guardian_provided",
            Confidence = 1.0,
            MemoryVersion = child.CurrentMemoryVersion,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.MemoryFacts.Add(fact);
        await _db.SaveChangesAsync(ct);
        return fact;
    }

    public async Task<bool> DeleteMemoryFactAsync(Guid familyId, Guid childProfileId, Guid memoryId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return false;

        var fact = await _db.MemoryFacts
            .FirstOrDefaultAsync(m => m.ChildProfileId == childProfileId && m.Id == memoryId, ct);
        if (fact is null) return false;

        fact.IsActive = false; // Soft delete ensures it's never re-injected
        child.CurrentMemoryVersion++;
        child.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("MemoryFact {MemoryId} deactivated for child {ChildId}", memoryId, childProfileId);
        return true;
    }
}
