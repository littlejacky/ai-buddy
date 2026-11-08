using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Application.FamilyManagement;

public sealed record CreateChildRequest(
    string Nickname,
    string AgeBand,
    string? PreferredVoice,
    string? LanguagePreference
);

public sealed record UpdateChildRequest(
    string? Nickname,
    string? AgeBand,
    string? PreferredVoice,
    string? LanguagePreference,
    bool? IsActive
);

public sealed record BindDeviceRequest(
    string DeviceIdentifier,
    string DeviceName,
    string Platform,
    Guid? BoundChildProfileId
);

public sealed class FamilyService
{
    private readonly BuddyDbContext _db;
    private readonly ILogger<FamilyService> _logger;

    public const int MaxChildrenPerFamily = 2; // Phase 1 default limit

    public FamilyService(BuddyDbContext db, ILogger<FamilyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Family?> GetFamilyAsync(Guid familyId, CancellationToken ct = default)
    {
        return await _db.Families
            .Include(f => f.Guardians)
            .Include(f => f.Children.Where(c => c.IsActive))
            .Include(f => f.Devices)
            .FirstOrDefaultAsync(f => f.Id == familyId, ct);
    }

    public async Task<ChildProfile?> GetChildProfileAsync(Guid familyId, Guid childProfileId, CancellationToken ct = default)
    {
        return await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
    }

    public async Task<ChildProfile> CreateChildProfileAsync(Guid familyId, CreateChildRequest request, CancellationToken ct = default)
    {
        var family = await _db.Families.FindAsync([familyId], ct);
        if (family is null)
        {
            family = new Family { Id = familyId, Name = "我的家庭" };
            _db.Families.Add(family);
        }

        var existingCount = await _db.ChildProfiles
            .CountAsync(c => c.FamilyId == familyId && c.IsActive, ct);

        if (existingCount >= MaxChildrenPerFamily)
        {
            throw new InvalidOperationException($"每个家庭最多支持创建 {MaxChildrenPerFamily} 个儿童档案。");
        }

        var child = new ChildProfile
        {
            FamilyId = familyId,
            Nickname = request.Nickname.Trim(),
            AgeBand = string.IsNullOrWhiteSpace(request.AgeBand) ? "7-8" : request.AgeBand,
            PreferredVoice = string.IsNullOrWhiteSpace(request.PreferredVoice) ? "marin" : request.PreferredVoice,
            LanguagePreference = string.IsNullOrWhiteSpace(request.LanguagePreference) ? "zh-CN" : request.LanguagePreference,
            CurrentMemoryVersion = 1,
            IsActive = true
        };

        _db.ChildProfiles.Add(child);

        // Pre-create standard consent records for this child
        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.FamilyId == familyId, ct);
        if (guardian is not null)
        {
            var standardConsents = new[]
            {
                ("VoiceService", true),
                ("CrossSessionMemory", false),
                ("ParentDiscoveryDigest", false),
                ("TranscriptProcessing", false),
                ("ChildSafetyAudit", true),
                ("ApproximateLocation", false),
                ("PublicWebSearch", false)
            };

            foreach (var (cType, isGranted) in standardConsents)
            {
                _db.ConsentRecords.Add(new ConsentRecord
                {
                    GuardianId = guardian.Id,
                    ChildProfile = child,
                    ConsentType = cType,
                    Version = "1.0",
                    IsGranted = isGranted,
                    GrantedAtUtc = isGranted ? DateTime.UtcNow : null
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return child;
    }

    public async Task<bool> UpdateChildProfileAsync(Guid familyId, Guid childProfileId, UpdateChildRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);

        if (child is null) return false;

        if (!string.IsNullOrWhiteSpace(request.Nickname)) child.Nickname = request.Nickname.Trim();
        if (!string.IsNullOrWhiteSpace(request.AgeBand)) child.AgeBand = request.AgeBand;
        if (!string.IsNullOrWhiteSpace(request.PreferredVoice)) child.PreferredVoice = request.PreferredVoice;
        if (!string.IsNullOrWhiteSpace(request.LanguagePreference)) child.LanguagePreference = request.LanguagePreference;
        if (request.IsActive.HasValue) child.IsActive = request.IsActive.Value;

        child.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteChildProfileAsync(Guid familyId, Guid childProfileId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);

        if (child is null) return false;

        // Soft delete child profile and disable associated active lease
        child.IsActive = false;
        child.UpdatedAtUtc = DateTime.UtcNow;

        var lease = await _db.ChildSessionLeases.FindAsync([childProfileId], ct);
        if (lease is not null)
        {
            _db.ChildSessionLeases.Remove(lease);
        }

        // Deactivate all memory facts
        var facts = await _db.MemoryFacts.Where(m => m.ChildProfileId == childProfileId).ToListAsync(ct);
        foreach (var f in facts) f.IsActive = false;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DeviceBinding> BindDeviceAsync(Guid familyId, BindDeviceRequest request, CancellationToken ct = default)
    {
        var existing = await _db.DeviceBindings
            .FirstOrDefaultAsync(d => d.FamilyId == familyId && d.DeviceIdentifier == request.DeviceIdentifier, ct);

        if (existing is not null)
        {
            existing.DeviceName = request.DeviceName;
            existing.Platform = request.Platform;
            existing.BoundChildProfileId = request.BoundChildProfileId;
            existing.LastActiveAtUtc = DateTime.UtcNow;
            existing.IsAuthorized = true;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var device = new DeviceBinding
        {
            FamilyId = familyId,
            DeviceIdentifier = request.DeviceIdentifier,
            DeviceName = request.DeviceName,
            Platform = request.Platform,
            BoundChildProfileId = request.BoundChildProfileId,
            IsAuthorized = true,
            LastActiveAtUtc = DateTime.UtcNow
        };

        _db.DeviceBindings.Add(device);
        await _db.SaveChangesAsync(ct);
        return device;
    }

    public async Task<bool> UnbindDeviceAsync(Guid familyId, Guid deviceBindingId, CancellationToken ct = default)
    {
        var device = await _db.DeviceBindings
            .FirstOrDefaultAsync(d => d.FamilyId == familyId && d.Id == deviceBindingId, ct);

        if (device is null) return false;

        device.IsAuthorized = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
