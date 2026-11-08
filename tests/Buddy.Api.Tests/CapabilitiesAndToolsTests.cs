using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Buddy.Api.Application.Capabilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Buddy.Api.Tests;

public class CapabilitiesAndToolsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CapabilitiesAndToolsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sqlite", "Data Source=test_caps.db");
            builder.UseSetting("AzureRealtime:Endpoint", "https://mock.openai.azure.com");
            builder.UseSetting("AzureRealtime:ApiKey", "mock-key");
            builder.UseSetting("AzureRealtime:DeploymentName", "gpt-realtime-2.1");
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetRegisteredTools_ReturnsAllTools()
    {
        var res = await _client.GetAsync("/api/tools");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var tools = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

        Assert.Contains("get_current_time", tools);
        Assert.Contains("calculate", tools);
        Assert.Contains("query_weather", tools);
        Assert.Contains("public_web_search", tools);
    }

    [Fact]
    public async Task CalculatorTool_CalculatesDeterministically()
    {
        var sessionId = Guid.NewGuid();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{sessionId}/tools/calculate/execute");
        req.Content = JsonContent.Create(new { expression = "25 * 4 + 50" });

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<BuddyToolResult>();
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Equal("150", body.Output);
    }

    [Fact]
    public async Task TimeTool_FormatsAccurately()
    {
        var sessionId = Guid.NewGuid();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{sessionId}/tools/get_current_time/execute");
        req.Content = JsonContent.Create(new { });

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<BuddyToolResult>();
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Contains("年", body.Output);
        Assert.Contains("星期", body.Output);
    }
}
