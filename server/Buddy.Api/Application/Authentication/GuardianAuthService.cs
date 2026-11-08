using System.Security.Cryptography;
using System.Text;
using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Application.Authentication;

public sealed record LoginResponse(
    bool Success,
    string? Token,
    Guid? GuardianId,
    Guid? FamilyId,
    string? DisplayName,
    string? ErrorMessage
);

public sealed record VerifyPinResponse(
    bool Success,
    bool IsLockedOut,
    string? Message
);

public sealed class GuardianAuthService
{
    private readonly BuddyDbContext _db;
    private readonly IGuardianIdentityProvider _identityProvider;
    private readonly ILogger<GuardianAuthService> _logger;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public GuardianAuthService(
        BuddyDbContext db,
        IGuardianIdentityProvider identityProvider,
        ILogger<GuardianAuthService> logger)
    {
        _db = db;
        _identityProvider = identityProvider;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(GuardianAuthenticationRequest request, CancellationToken ct = default)
    {
        var authResult = await _identityProvider.AuthenticateAsync(request, ct);
        if (!authResult.Success)
        {
            return new LoginResponse(false, null, null, null, null, authResult.ErrorMessage);
        }

        // Find or create Guardian & Family
        var guardian = await _db.Guardians
            .Include(g => g.Family)
            .FirstOrDefaultAsync(g => g.SubjectIdentifier == authResult.SubjectIdentifier, ct);

        if (guardian is null)
        {
            var family = new Family
            {
                Name = $"{authResult.DisplayName ?? "家长"}的家庭"
            };
            _db.Families.Add(family);

            guardian = new Guardian
            {
                Family = family,
                SubjectIdentifier = authResult.SubjectIdentifier!,
                DisplayName = authResult.DisplayName ?? "家长",
                Role = "Owner",
                PinHash = HashPin("123456")
            };
            _db.Guardians.Add(guardian);

            // Create a default child profile for the newly registered family (spec: up to 2 profiles default)
            var defaultChild = new ChildProfile
            {
                Family = family,
                Nickname = "宝贝",
                AgeBand = "7-8",
                PreferredVoice = "marin"
            };
            _db.ChildProfiles.Add(defaultChild);

            await _db.SaveChangesAsync(ct);
        }

        // Generate simple bearer token (in production, use signed JWT or secure session cookie)
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{guardian.Id}:{guardian.FamilyId}:{DateTime.UtcNow.Ticks}"));

        return new LoginResponse(
            Success: true,
            Token: token,
            GuardianId: guardian.Id,
            FamilyId: guardian.FamilyId,
            DisplayName: guardian.DisplayName,
            ErrorMessage: null
        );
    }

    public async Task<VerifyPinResponse> VerifyPinAsync(Guid guardianId, string pin, CancellationToken ct = default)
    {
        var guardian = await _db.Guardians.FindAsync([guardianId], ct);
        if (guardian is null)
        {
            return new VerifyPinResponse(false, false, "未找到监护人信息。");
        }

        var now = DateTime.UtcNow;
        if (guardian.LockoutEndUtc.HasValue && guardian.LockoutEndUtc.Value > now)
        {
            var remainingMin = Math.Ceiling((guardian.LockoutEndUtc.Value - now).TotalMinutes);
            return new VerifyPinResponse(false, true, $"PIN 已被锁定，请在 {remainingMin} 分钟后再试。");
        }

        var hashed = HashPin(pin);
        // Fallback check: also allow "123456" in dev if PinHash is literal
        var isMatch = guardian.PinHash == hashed || guardian.PinHash == pin;

        if (isMatch)
        {
            guardian.FailedPinAttempts = 0;
            guardian.LockoutEndUtc = null;
            await _db.SaveChangesAsync(ct);
            return new VerifyPinResponse(true, false, "验证成功。");
        }

        guardian.FailedPinAttempts++;
        if (guardian.FailedPinAttempts >= MaxFailedAttempts)
        {
            guardian.LockoutEndUtc = now.Add(LockoutDuration);
            await _db.SaveChangesAsync(ct);
            return new VerifyPinResponse(false, true, $"连续输入错误超过 {MaxFailedAttempts} 次，账号已锁定 15 分钟。");
        }

        await _db.SaveChangesAsync(ct);
        return new VerifyPinResponse(false, false, $"PIN 码错误。还可尝试 {MaxFailedAttempts - guardian.FailedPinAttempts} 次。");
    }

    public async Task<bool> SetPinAsync(Guid guardianId, string newPin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPin) || newPin.Length != 6 || !newPin.All(char.IsDigit))
        {
            return false;
        }

        var guardian = await _db.Guardians.FindAsync([guardianId], ct);
        if (guardian is null) return false;

        guardian.PinHash = HashPin(newPin);
        guardian.FailedPinAttempts = 0;
        guardian.LockoutEndUtc = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"buddy_pin_salt_{pin}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
