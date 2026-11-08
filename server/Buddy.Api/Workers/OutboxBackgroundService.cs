using System.Text.Json;
using Buddy.Api.Application.Memory;
using Buddy.Api.Application.Safety;
using Buddy.Api.Application.Summary;
using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Workers;

public sealed class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxBackgroundService> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    public OutboxBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OutboxBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxBackgroundService started with InstanceId: {InstanceId}", _instanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny = await ProcessNextBatchAsync(stoppingToken);
                if (!processedAny)
                {
                    await Task.Delay(2000, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in OutboxBackgroundService loop");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxBackgroundService stopped.");
    }

    public async Task<bool> ProcessNextBatchAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuddyDbContext>();

        var now = DateTime.UtcNow;

        // Atomically claim a pending or timed-out job
        var job = await db.OutboxJobs
            .Where(j => j.Status == "Pending" || (j.Status == "Processing" && j.LockedUntilUtc < now))
            .OrderBy(j => j.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (job is null) return false;

        job.Status = "Processing";
        job.LockInstanceId = _instanceId;
        job.LockedUntilUtc = now.AddMinutes(2); // 2 minute processing lease
        job.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);

        try
        {
            await ExecuteJobAsync(scope.ServiceProvider, job, ct);

            job.Status = "Completed";
            job.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("OutboxJob {JobId} ({JobType}) completed successfully", job.Id, job.JobType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OutboxJob {JobId} execution failed", job.Id);
            job.RetryCount++;
            job.LastError = ex.Message;
            job.Status = job.RetryCount >= job.MaxRetries ? "Failed" : "Pending";
            job.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    private static async Task ExecuteJobAsync(IServiceProvider sp, OutboxJob job, CancellationToken ct)
    {
        var db = sp.GetRequiredService<BuddyDbContext>();
        var digestService = sp.GetRequiredService<DiscoveryDigestService>();
        var safetyService = sp.GetRequiredService<SafetyAuditService>();
        var memoryService = sp.GetRequiredService<MemoryService>();

        if (job.JobType == "ProcessSessionFinalize")
        {
            using var doc = JsonDocument.Parse(job.PayloadJson);
            var root = doc.RootElement;
            var sessionId = root.GetProperty("SessionId").GetGuid();
            var childProfileId = root.GetProperty("ChildProfileId").GetGuid();
            var familyId = root.GetProperty("FamilyId").GetGuid();

            var session = await db.Sessions.FindAsync([sessionId], ct);
            if (session is null || string.IsNullOrWhiteSpace(session.StructuredTranscriptJson))
            {
                return;
            }

            // 1. Generate Parent Discovery Digest
            await digestService.GenerateDigestFromTranscriptAsync(
                sessionId,
                childProfileId,
                session.StructuredTranscriptJson,
                ct);

            // 2. Scan Transcript for Child Safety
            await safetyService.ScanTranscriptForSafetyAsync(
                sessionId,
                childProfileId,
                session.StructuredTranscriptJson,
                ct);

            // 3. Extract Memory Facts (L1 / L3 heuristics)
            await ExtractMemoriesAsync(memoryService, familyId, childProfileId, session.StructuredTranscriptJson, ct);
        }
    }

    private static async Task ExtractMemoriesAsync(
        MemoryService memoryService,
        Guid familyId,
        Guid childProfileId,
        string structuredTranscriptJson,
        CancellationToken ct)
    {
        List<JsonElement>? turns = null;
        try
        {
            turns = JsonSerializer.Deserialize<List<JsonElement>>(structuredTranscriptJson);
        }
        catch { return; }

        if (turns is null) return;

        foreach (var t in turns)
        {
            if (t.TryGetProperty("role", out var r) && r.GetString() == "user" &&
                t.TryGetProperty("text", out var txt))
            {
                var speech = txt.GetString() ?? "";
                // Heuristic fact extraction: "我最喜欢...", "我喜欢...", "我有一只..."
                if (speech.Contains("我最喜欢") || speech.Contains("我超级喜欢"))
                {
                    var target = speech[(speech.IndexOf("喜欢") + 2)..].Trim('！', '。', ' ', '，');
                    if (target.Length is > 1 and < 20)
                    {
                        await memoryService.AddMemoryFactAsync(
                            familyId,
                            childProfileId,
                            new CreateMemoryRequest("Interest", $"特别喜欢{target}", "child_explicit"),
                            ct);
                    }
                }
            }
        }
    }
}
