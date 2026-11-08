namespace Buddy.Api.Application.Authentication;

public sealed record GuardianAuthenticationRequest(
    string SubjectIdentifier,
    string AuthCode,
    string Provider = "stub"
);

public sealed record GuardianIdentityResult(
    bool Success,
    string? SubjectIdentifier = null,
    string? DisplayName = null,
    string? ErrorMessage = null
);

public interface IGuardianIdentityProvider
{
    string ProviderName { get; }

    Task<GuardianIdentityResult> AuthenticateAsync(
        GuardianAuthenticationRequest request,
        CancellationToken ct = default);
}
