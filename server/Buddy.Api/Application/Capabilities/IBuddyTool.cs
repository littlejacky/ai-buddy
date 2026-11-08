using System.Text.Json;

namespace Buddy.Api.Application.Capabilities;

public sealed record BuddyToolContext(
    Guid SessionId,
    Guid ChildProfileId,
    Guid FamilyId,
    string? UserLocationCity = null,
    string? TimeZone = "Asia/Shanghai"
);

public sealed record BuddyToolResult(
    bool Success,
    string Output,
    string? FormattedForChild = null,
    string? SourceUrl = null,
    string? ErrorMessage = null
);

public interface IBuddyTool
{
    string Name { get; }
    string Description { get; }
    string RequiredConsent { get; }

    Task<BuddyToolResult> ExecuteAsync(
        BuddyToolContext context,
        JsonElement arguments,
        CancellationToken ct = default);
}

public sealed record PublicSearchRequest(
    string Query,
    int MaxResults = 3
);

public sealed record SearchItem(
    string Title,
    string Snippet,
    string Url,
    string? PublishedAt
);

public sealed record PublicSearchResult(
    bool Success,
    List<SearchItem> Items,
    string? ErrorMessage = null
);

public interface IPublicSearchProvider
{
    Task<PublicSearchResult> SearchAsync(PublicSearchRequest request, CancellationToken ct = default);
}
