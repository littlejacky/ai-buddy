namespace Buddy.Api.Application.Authentication;

/// <summary>
/// Development-only authentication provider stub.
/// Strictly forbidden to operate in production environments.
/// </summary>
public sealed class DevelopmentGuardianIdentityProvider : IGuardianIdentityProvider
{
    private readonly IHostEnvironment _env;

    public string ProviderName => "DevelopmentStub";

    public DevelopmentGuardianIdentityProvider(IHostEnvironment env)
    {
        _env = env;
    }

    public Task<GuardianIdentityResult> AuthenticateAsync(
        GuardianAuthenticationRequest request,
        CancellationToken ct = default)
    {
        if (!_env.IsDevelopment())
        {
            return Task.FromResult(new GuardianIdentityResult(
                Success: false,
                ErrorMessage: "Development authentication stub is strictly prohibited outside Development environment."
            ));
        }

        if (string.IsNullOrWhiteSpace(request.SubjectIdentifier))
        {
            return Task.FromResult(new GuardianIdentityResult(
                Success: false,
                ErrorMessage: "Subject identifier cannot be empty."
            ));
        }

        // In development stub, "123456" is the universal test code
        if (request.AuthCode != "123456")
        {
            return Task.FromResult(new GuardianIdentityResult(
                Success: false,
                ErrorMessage: "Invalid verification code. In development mode, use 123456."
            ));
        }

        return Task.FromResult(new GuardianIdentityResult(
            Success: true,
            SubjectIdentifier: request.SubjectIdentifier,
            DisplayName: "测试家长"
        ));
    }
}
