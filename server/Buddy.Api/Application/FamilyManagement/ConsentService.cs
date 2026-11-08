using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Application.FamilyManagement;

public sealed record UpdateConsentRequest(
    string ConsentType,
    bool IsGranted
);

public sealed record ConsentStatusDto(
    string ConsentType,
    bool IsGranted,
    string Version,
    DateTime? GrantedAtUtc,
    DateTime? RevokedAtUtc
);

public sealed class ConsentService
{
    private readonly BuddyDbContext _db;
    private readonly ILogger<ConsentService> _logger;

    public ConsentService(BuddyDbContext db, ILogger<ConsentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ConsentStatusDto>> GetConsentsAsync(Guid familyId, Guid childProfileId, CancellationToken ct = default)
    {
        // Enforce family isolation
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return [];

        var records = await _db.ConsentRecords
            .Where(c => c.ChildProfileId == childProfileId)
            .ToListAsync(ct);

        return records.Select(r => new ConsentStatusDto(
            r.ConsentType,
            r.IsGranted,
            r.Version,
            r.GrantedAtUtc,
            r.RevokedAtUtc
        )).ToList();
    }

    public async Task<bool> UpdateConsentAsync(
        Guid familyId,
        Guid guardianId,
        Guid childProfileId,
        UpdateConsentRequest request,
        CancellationToken ct = default)
    {
        // Enforce family isolation
        var child = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FamilyId == familyId && c.Id == childProfileId, ct);
        if (child is null) return false;

        var existing = await _db.ConsentRecords
            .FirstOrDefaultAsync(c => c.ChildProfileId == childProfileId && c.ConsentType == request.ConsentType, ct);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            _db.ConsentRecords.Add(new ConsentRecord
            {
                GuardianId = guardianId,
                ChildProfileId = childProfileId,
                ConsentType = request.ConsentType,
                Version = "1.0",
                IsGranted = request.IsGranted,
                GrantedAtUtc = request.IsGranted ? now : null,
                RevokedAtUtc = request.IsGranted ? null : now
            });
        }
        else
        {
            existing.IsGranted = request.IsGranted;
            if (request.IsGranted)
            {
                existing.GrantedAtUtc = now;
                existing.RevokedAtUtc = null;
            }
            else
            {
                existing.RevokedAtUtc = now;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Consent {ConsentType} for child {ChildId} updated to {IsGranted}",
            request.ConsentType, childProfileId, request.IsGranted);

        return true;
    }

    public async Task<bool> IsConsentGrantedAsync(Guid childProfileId, string consentType, CancellationToken ct = default)
    {
        var consent = await _db.ConsentRecords
            .FirstOrDefaultAsync(c => c.ChildProfileId == childProfileId && c.ConsentType == consentType, ct);

        return consent?.IsGranted ?? false;
    }
}
