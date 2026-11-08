using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Buddy.Api.Application.Authentication;
using Buddy.Api.Application.FamilyManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Buddy.Api.Tests;

public class AuthAndFamilyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthAndFamilyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sqlite", "Data Source=test_auth.db");
            builder.UseSetting("AzureRealtime:Endpoint", "https://mock.openai.azure.com");
            builder.UseSetting("AzureRealtime:ApiKey", "mock-key");
            builder.UseSetting("AzureRealtime:DeploymentName", "gpt-realtime-2.1");
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GuardianLogin_WithValidDevCode_ReturnsSuccessAndFamilyId()
    {
        var req = new GuardianAuthenticationRequest("test_parent@example.com", "123456");
        var res = await _client.PostAsJsonAsync("/api/auth/guardian/login", req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.NotNull(body.GuardianId);
        Assert.NotNull(body.FamilyId);
    }

    [Fact]
    public async Task GuardianLogin_WithInvalidDevCode_ReturnsBadRequest()
    {
        var req = new GuardianAuthenticationRequest("test_parent@example.com", "wrong_code");
        var res = await _client.PostAsJsonAsync("/api/auth/guardian/login", req);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(body.Success);
    }

    [Fact]
    public async Task FamilyIsolation_CannotAccessOtherFamilyChild()
    {
        // Family 1
        var f1Id = Guid.NewGuid();
        var f2Id = Guid.NewGuid();

        // Create child in Family 1
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/children");
        req.Headers.Add("X-Family-Id", f1Id.ToString());
        req.Content = JsonContent.Create(new CreateChildRequest("小明", "7-8", "marin", "zh-CN"));
        var createRes = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var childId = doc.RootElement.GetProperty("id").GetString();

        // Try to access child with Family 2 header -> Expect 404 (isolation enforced)
        var accessReq = new HttpRequestMessage(HttpMethod.Get, $"/api/children/{childId}");
        accessReq.Headers.Add("X-Family-Id", f2Id.ToString());
        var accessRes = await _client.SendAsync(accessReq);

        Assert.Equal(HttpStatusCode.NotFound, accessRes.StatusCode);
    }

    [Fact]
    public async Task ConsentManagement_CanToggleAndVerify()
    {
        var fId = Guid.NewGuid();
        // Create child
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/children");
        createReq.Headers.Add("X-Family-Id", fId.ToString());
        createReq.Content = JsonContent.Create(new CreateChildRequest("乐乐", "4-6", "marin", "zh-CN"));
        var createRes = await _client.SendAsync(createReq);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var childId = doc.RootElement.GetProperty("id").GetString();

        // Read consents
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/children/{childId}/consents");
        getReq.Headers.Add("X-Family-Id", fId.ToString());
        var getRes = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        // Update Consent: Grant CrossSessionMemory
        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/api/children/{childId}/consents");
        putReq.Headers.Add("X-Family-Id", fId.ToString());
        putReq.Content = JsonContent.Create(new UpdateConsentRequest("CrossSessionMemory", true));
        var putRes = await _client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        // Verify it was updated
        var verifyReq = new HttpRequestMessage(HttpMethod.Get, $"/api/children/{childId}/consents");
        verifyReq.Headers.Add("X-Family-Id", fId.ToString());
        var verifyRes = await _client.SendAsync(verifyReq);
        var consents = await verifyRes.Content.ReadFromJsonAsync<List<ConsentStatusDto>>();
        Assert.NotNull(consents);
        var memConsent = consents.FirstOrDefault(c => c.ConsentType == "CrossSessionMemory");
        Assert.NotNull(memConsent);
        Assert.True(memConsent.IsGranted);
    }
}
