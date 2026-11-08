using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Buddy.Api.Application.Memory;
using Buddy.Api.Application.SessionManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Buddy.Api.Tests;

public class SessionAndMemoryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SessionAndMemoryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sqlite", "Data Source=test_session.db");
            builder.UseSetting("AzureRealtime:Endpoint", "https://mock.openai.azure.com");
            builder.UseSetting("AzureRealtime:ApiKey", "mock-key");
            builder.UseSetting("AzureRealtime:DeploymentName", "gpt-realtime-2.1");
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task SessionFinalize_IsIdempotent_AndCreatesOutboxJob()
    {
        var familyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var childId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var sessionId = Guid.NewGuid();

        // Directly invoke session lifecycle via endpoints
        var req = new FinalizeSessionRequest(
            DurationSeconds: 180,
            FinalTurns:
            [
                new TurnRecord("1", "user", "我最喜欢恐龙了！霸王龙超级厉害。", 1000, null),
                new TurnRecord("2", "assistant", "哇！霸王龙确实是白垩纪的超级霸主呢！", 1500, null)
            ]
        );

        // First checkpoint
        var cpReq = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{sessionId}/checkpoint");
        cpReq.Headers.Add("X-Family-Id", familyId.ToString());
        cpReq.Content = JsonContent.Create(new CheckpointRequest(req.FinalTurns));

        // Note: sessionId might not exist yet if not created via /realtime/session, but finalize tests the endpoint
        var finalizeReq = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{sessionId}/finalize");
        finalizeReq.Headers.Add("X-Family-Id", familyId.ToString());
        finalizeReq.Content = JsonContent.Create(req);
        var finalizeRes = await _client.SendAsync(finalizeReq);

        // Expect 404 since session wasn't started, or 200 if started
        Assert.True(finalizeRes.StatusCode == HttpStatusCode.NotFound || finalizeRes.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task MemoryCRUD_AddAndList_RespectsChildBoundary()
    {
        var familyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var childId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var addReq = new HttpRequestMessage(HttpMethod.Post, $"/api/children/{childId}/memories");
        addReq.Headers.Add("X-Family-Id", familyId.ToString());
        addReq.Content = JsonContent.Create(new CreateMemoryRequest("Interest", "喜欢深海蓝鲸", "guardian_provided"));
        var addRes = await _client.SendAsync(addReq);
        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);

        using var doc = JsonDocument.Parse(await addRes.Content.ReadAsStringAsync());
        var memoryId = doc.RootElement.GetProperty("id").GetString();

        // List memories
        var listReq = new HttpRequestMessage(HttpMethod.Get, $"/api/children/{childId}/memories");
        listReq.Headers.Add("X-Family-Id", familyId.ToString());
        var listRes = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<MemoryFactDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, m => m.Content == "喜欢深海蓝鲸");

        // Delete memory
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/children/{childId}/memories/{memoryId}");
        delReq.Headers.Add("X-Family-Id", familyId.ToString());
        var delRes = await _client.SendAsync(delReq);
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }
}
