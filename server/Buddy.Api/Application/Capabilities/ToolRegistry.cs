using System.Text.Json;
using Buddy.Api.Application.FamilyManagement;
using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;

namespace Buddy.Api.Application.Capabilities;

public sealed class DevelopmentSearchProvider : IPublicSearchProvider
{
    public Task<PublicSearchResult> SearchAsync(PublicSearchRequest request, CancellationToken ct = default)
    {
        // Safe curated mock knowledge entries for development / evaluation
        var items = new List<SearchItem>();
        var q = request.Query.ToLowerInvariant();

        if (q.Contains("鲸鱼") || q.Contains("蓝鲸"))
        {
            items.Add(new SearchItem(
                "海洋百科：蓝鲸是地球上最大的动物",
                "成年蓝鲸体长可达 30 米以上，体重达 150~180 吨。因为海水拥有巨大的浮力支撑其庞大身躯，所以它们能在水里自如游弋。",
                "https://baike.example.com/blue-whale",
                "2026-05-01"
            ));
        }
        else if (q.Contains("恐龙") || q.Contains("霸王龙"))
        {
            items.Add(new SearchItem(
                "古生物百科：霸王龙生存年代与习性",
                "霸王龙（Tyrannosaurus Rex）生存于白垩纪末期，距今约 6800 万年到 6600 万年，是已知的最凶猛的陆生肉食性恐龙之一。",
                "https://baike.example.com/t-rex",
                "2026-03-15"
            ));
        }
        else
        {
            items.Add(new SearchItem(
                $"关于【{request.Query}】的探索发现",
                $"根据最新科学常识，【{request.Query}】是自然与科学探索领域中非常有趣的现象。",
                "https://baike.example.com/science-kids",
                "2026-01-01"
            ));
        }

        return Task.FromResult(new PublicSearchResult(true, items));
    }
}

public sealed class WeatherTool : IBuddyTool
{
    public string Name => "query_weather";
    public string Description => "查询指定城市的天气与温度。参数 city 为城市名称，如 '北京'、'上海'。";
    public string RequiredConsent => "ApproximateLocation";

    public Task<BuddyToolResult> ExecuteAsync(
        BuddyToolContext context,
        JsonElement arguments,
        CancellationToken ct = default)
    {
        string city = "北京";
        if (arguments.TryGetProperty("city", out var cProp) && !string.IsNullOrWhiteSpace(cProp.GetString()))
        {
            city = cProp.GetString()!;
        }
        else if (!string.IsNullOrWhiteSpace(context.UserLocationCity))
        {
            city = context.UserLocationCity;
        }

        var report = $"{city}今天晴朗，气温 18 到 24 摄氏度，空气清新，适合户外活动。（观测时间：今天上午 09:00）";
        var childSpeech = $"{city}今天天气特别好，晴空万里，温度很舒服哦！";

        return Task.FromResult(new BuddyToolResult(
            Success: true,
            Output: report,
            FormattedForChild: childSpeech
        ));
    }
}

public sealed class PublicSearchTool : IBuddyTool
{
    private readonly IPublicSearchProvider _searchProvider;

    public string Name => "public_web_search";
    public string Description => "在遇到最新科学发现、百科知识或外部公开事实时发起网络知识检索。参数 query 为搜索关键词。";
    public string RequiredConsent => "PublicWebSearch";

    public PublicSearchTool(IPublicSearchProvider searchProvider)
    {
        _searchProvider = searchProvider;
    }

    public async Task<BuddyToolResult> ExecuteAsync(
        BuddyToolContext context,
        JsonElement arguments,
        CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("query", out var qProp) || string.IsNullOrWhiteSpace(qProp.GetString()))
        {
            return new BuddyToolResult(false, "", ErrorMessage: "Missing 'query' parameter.");
        }

        var query = qProp.GetString()!;
        var searchResult = await _searchProvider.SearchAsync(new PublicSearchRequest(query), ct);

        if (!searchResult.Success || searchResult.Items.Count == 0)
        {
            return new BuddyToolResult(false, "", ErrorMessage: "未能找到相关可信公开资料。");
        }

        var top = searchResult.Items[0];
        var output = $"来源：{top.Title}（{top.Url}，发布时间：{top.PublishedAt}）\n内容摘要：{top.Snippet}";
        var childSpeech = $"我查到啦！{top.Snippet}";

        return new BuddyToolResult(
            Success: true,
            Output: output,
            FormattedForChild: childSpeech,
            SourceUrl: top.Url
        );
    }
}

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IBuddyTool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConsentService _consentService;
    private readonly BuddyDbContext _db;
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(
        IEnumerable<IBuddyTool> tools,
        ConsentService consentService,
        BuddyDbContext db,
        ILogger<ToolRegistry> logger)
    {
        foreach (var t in tools)
        {
            _tools[t.Name] = t;
        }
        _consentService = consentService;
        _db = db;
        _logger = logger;
    }

    public IEnumerable<IBuddyTool> GetAllTools() => _tools.Values;

    public async Task<BuddyToolResult> ExecuteToolAsync(
        string toolName,
        BuddyToolContext context,
        JsonElement arguments,
        CancellationToken ct = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return new BuddyToolResult(false, "", ErrorMessage: $"Tool '{toolName}' is not registered.");
        }

        // Verify guardian consent for the tool
        if (!string.IsNullOrWhiteSpace(tool.RequiredConsent) && tool.RequiredConsent != "VoiceService")
        {
            var isGranted = await _consentService.IsConsentGrantedAsync(context.ChildProfileId, tool.RequiredConsent, ct);
            if (!isGranted)
            {
                _logger.LogWarning("Execution of tool {Tool} rejected: Consent {Consent} not granted.", toolName, tool.RequiredConsent);
                return new BuddyToolResult(false, "", ErrorMessage: $"监护人未授权【{tool.RequiredConsent}】功能，无法使用此工具。");
            }
        }

        var start = DateTime.UtcNow;
        var result = await tool.ExecuteAsync(context, arguments, ct);
        var duration = (int)(DateTime.UtcNow - start).TotalMilliseconds;

        // Audit tool invocation in database
        try
        {
            _db.ToolInvocations.Add(new ToolInvocation
            {
                SessionId = context.SessionId,
                ToolName = toolName,
                InputJson = arguments.ToString(),
                OutputJson = result.Output,
                IsSuccess = result.Success,
                DurationMs = duration,
                InvokedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to audit tool invocation {Tool}", toolName);
        }

        return result;
    }
}
