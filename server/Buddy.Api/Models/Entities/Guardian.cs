namespace Buddy.Api.Models.Entities;

public sealed class Guardian
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public Family? Family { get; set; }

    public string SubjectIdentifier { get; set; } = string.Empty; // e.g. Phone, Email or Id
    public string DisplayName { get; set; } = "家长";
    public string Role { get; set; } = "Owner"; // Owner, Parent
    public string PinHash { get; set; } = string.Empty; // SHA-256 / PBKDF2 hash of 6-digit PIN
    public int FailedPinAttempts { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ConsentRecord> Consents { get; set; } = [];
}
