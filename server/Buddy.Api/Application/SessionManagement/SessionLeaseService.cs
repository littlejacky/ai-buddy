using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Application.SessionManagement;

public sealed record LeaseAcquisitionResult(
    bool Acquired,
    string? HeartbeatToken,
    string? RejectionReason
);

public sealed class SessionLeaseService
{
    private readonly BuddyDbContext _db;
    private readonly ILogger<SessionLeaseService> _logger;

    // Default lease duration: 30 seconds, renewed by client heartbeats
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    public SessionLeaseService(BuddyDbContext db, ILogger<SessionLeaseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LeaseAcquisitionResult> TryAcquireLeaseAsync(
        Guid childProfileId,
        Guid sessionId,
        Guid? deviceBindingId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existingLease = await _db.ChildSessionLeases.FindAsync([childProfileId], ct);

        if (existingLease is null)
        {
            var token = Guid.NewGuid().ToString("N");
            var lease = new ChildSessionLease
            {
                ChildProfileId = childProfileId,
                ActiveSessionId = sessionId,
                DeviceBindingId = deviceBindingId,
                HeartbeatToken = token,
                AcquiredAtUtc = now,
                LeaseExpiresAtUtc = now.Add(LeaseDuration)
            };
            _db.ChildSessionLeases.Add(lease);
            await _db.SaveChangesAsync(ct);
            return new LeaseAcquisitionResult(true, token, null);
        }

        // Check if existing lease has expired or belongs to same session
        if (existingLease.LeaseExpiresAtUtc <= now || existingLease.ActiveSessionId == sessionId)
        {
            var token = Guid.NewGuid().ToString("N");
            existingLease.ActiveSessionId = sessionId;
            existingLease.DeviceBindingId = deviceBindingId;
            existingLease.HeartbeatToken = token;
            existingLease.AcquiredAtUtc = now;
            existingLease.LeaseExpiresAtUtc = now.Add(LeaseDuration);
            await _db.SaveChangesAsync(ct);
            return new LeaseAcquisitionResult(true, token, null);
        }

        // Another active device currently holds the lease
        _logger.LogWarning("Child {ChildId} is already active on another device (session {ActiveSession})",
            childProfileId, existingLease.ActiveSessionId);

        return new LeaseAcquisitionResult(
            false,
            null,
            "Buddy 正在另一台设备上陪伴你哦。"
        );
    }

    public async Task<bool> RenewLeaseAsync(Guid childProfileId, Guid sessionId, string heartbeatToken, CancellationToken ct = default)
    {
        var lease = await _db.ChildSessionLeases.FindAsync([childProfileId], ct);
        if (lease is null || lease.ActiveSessionId != sessionId || lease.HeartbeatToken != heartbeatToken)
        {
            return false;
        }

        lease.LeaseExpiresAtUtc = DateTime.UtcNow.Add(LeaseDuration);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReleaseLeaseAsync(Guid childProfileId, Guid sessionId, CancellationToken ct = default)
    {
        var lease = await _db.ChildSessionLeases.FindAsync([childProfileId], ct);
        if (lease is not null && lease.ActiveSessionId == sessionId)
        {
            _db.ChildSessionLeases.Remove(lease);
            await _db.SaveChangesAsync(ct);
        }
    }
}
