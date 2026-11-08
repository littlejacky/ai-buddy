using System.Text.Json;

namespace Buddy.Api.Application.Capabilities;

public sealed class TimeTool : IBuddyTool
{
    public string Name => "get_current_time";
    public string Description => "获取当前设备所在的本地准确时间、公历日期、星期几。用于回答小朋友'今天几号'、'星期几'、'现在几点'等问题。";
    public string RequiredConsent => "VoiceService";

    public Task<BuddyToolResult> ExecuteAsync(
        BuddyToolContext context,
        JsonElement arguments,
        CancellationToken ct = default)
    {
        var tzId = context.TimeZone ?? "Asia/Shanghai";
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var weekdaysZh = new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        var weekday = weekdaysZh[(int)localNow.DayOfWeek];

        var formatted = $"{localNow.Year}年{localNow.Month}月{localNow.Day}日 {weekday} {localNow:HH:mm}";
        var childSpeech = $"现在是北京时间{localNow.Month}月{localNow.Day}号，{weekday}，时间是{localNow.Hour}点{localNow.Minute}分。";

        return Task.FromResult(new BuddyToolResult(
            Success: true,
            Output: formatted,
            FormattedForChild: childSpeech
        ));
    }
}

public sealed class CalculatorTool : IBuddyTool
{
    public string Name => "calculate";
    public string Description => "执行确定性数学四则运算或单位换算。参数 expression 为数学算式，例如 '125 + 380' 或 '5 * 12'。";
    public string RequiredConsent => "VoiceService";

    public Task<BuddyToolResult> ExecuteAsync(
        BuddyToolContext context,
        JsonElement arguments,
        CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("expression", out var exprProp))
        {
            return Task.FromResult(new BuddyToolResult(false, "", ErrorMessage: "Missing 'expression' argument."));
        }

        var expr = exprProp.GetString() ?? "";
        // Simple deterministic math evaluator for basic operations
        try
        {
            var dataTable = new System.Data.DataTable();
            var result = dataTable.Compute(expr, null);
            return Task.FromResult(new BuddyToolResult(
                Success: true,
                Output: result?.ToString() ?? "0",
                FormattedForChild: $"算出来啦，结果是 {result}。"
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new BuddyToolResult(false, "", ErrorMessage: $"计算错误: {ex.Message}"));
        }
    }
}
