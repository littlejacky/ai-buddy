using System.Text.Json;
using Buddy.Api.Application.Authentication;
using Buddy.Api.Application.Capabilities;
using Buddy.Api.Application.FamilyManagement;
using Buddy.Api.Application.Memory;
using Buddy.Api.Application.Safety;
using Buddy.Api.Application.SessionManagement;
using Buddy.Api.Application.Summary;
using Buddy.Api.Models;
using Buddy.Api.Models.Entities;
using Buddy.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Buddy.Api.Endpoints;

public static class BuddyEndpointExtensions
{
    public static void MapBuddyEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ──────────────────────── 1. Auth & Guardian ────────────────────────
        var auth = api.MapGroup("/auth/guardian").WithTags("GuardianAuth");

        auth.MapPost("/login", async (
            GuardianAuthenticationRequest req,
            GuardianAuthService authService,
            CancellationToken ct) =>
        {
            var res = await authService.LoginAsync(req, ct);
            return res.Success ? Results.Ok(res) : Results.BadRequest(res);
        });

        auth.MapPost("/verify-pin", async (
            [FromHeader(Name = "X-Guardian-Id")] Guid? guardianId,
            [FromBody] JsonElement body,
            GuardianAuthService authService,
            CancellationToken ct) =>
        {
            var gid = guardianId ?? Guid.Parse("22222222-2222-2222-2222-222222222222"); // Default dev guardian
            var pin = body.TryGetProperty("pin", out var p) ? p.GetString() ?? "" : "";
            var res = await authService.VerifyPinAsync(gid, pin, ct);
            return res.Success ? Results.Ok(res) : Results.BadRequest(res);
        });

        auth.MapPost("/set-pin", async (
            [FromHeader(Name = "X-Guardian-Id")] Guid? guardianId,
            [FromBody] JsonElement body,
            GuardianAuthService authService,
            CancellationToken ct) =>
        {
            var gid = guardianId ?? Guid.Parse("22222222-2222-2222-2222-222222222222");
            var pin = body.TryGetProperty("pin", out var p) ? p.GetString() ?? "" : "";
            var ok = await authService.SetPinAsync(gid, pin, ct);
            return ok ? Results.Ok(new { success = true }) : Results.BadRequest(new { error = "PIN 必须为 6 位纯数字。" });
        });

        // ──────────────────────── 2. Family & Children ────────────────────────
        api.MapGet("/families/current", async (
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            FamilyService familyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"); // Default dev family
            var family = await familyService.GetFamilyAsync(fid, ct);
            if (family is null) return Results.NotFound(new { error = "家庭信息未找到。" });
            return Results.Ok(family);
        }).WithTags("Families");

        var children = api.MapGroup("/children").WithTags("Children");

        children.MapPost("/", async (
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            CreateChildRequest req,
            FamilyService familyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            try
            {
                var child = await familyService.CreateChildProfileAsync(fid, req, ct);
                return Results.Created($"/api/children/{child.Id}", child);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        children.MapGet("/{childId:guid}", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            FamilyService familyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var child = await familyService.GetChildProfileAsync(fid, childId, ct);
            return child is not null ? Results.Ok(child) : Results.NotFound();
        });

        children.MapPut("/{childId:guid}", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            UpdateChildRequest req,
            FamilyService familyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var ok = await familyService.UpdateChildProfileAsync(fid, childId, req, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        children.MapDelete("/{childId:guid}", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            FamilyService familyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var ok = await familyService.DeleteChildProfileAsync(fid, childId, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        // Consents
        children.MapGet("/{childId:guid}/consents", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            ConsentService consentService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var consents = await consentService.GetConsentsAsync(fid, childId, ct);
            return Results.Ok(consents);
        });

        children.MapPut("/{childId:guid}/consents", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            [FromHeader(Name = "X-Guardian-Id")] Guid? guardianId,
            UpdateConsentRequest req,
            ConsentService consentService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var gid = guardianId ?? Guid.Parse("22222222-2222-2222-2222-222222222222");
            var ok = await consentService.UpdateConsentAsync(fid, gid, childId, req, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        // ──────────────────────── 3. Devices ────────────────────────
        var devices = api.MapGroup("/devices").WithTags("Devices");

        devices.MapPost("/bind", async (
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            BindDeviceRequest req,
            FamilyService familyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var binding = await familyService.BindDeviceAsync(fid, req, ct);
            return Results.Ok(binding);
        });

        devices.MapPost("/{deviceBindingId:guid}/unbind", async (
            Guid deviceBindingId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            FamilyService familyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var ok = await familyService.UnbindDeviceAsync(fid, deviceBindingId, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        // ──────────────────────── 4. Realtime Sessions & Leases ────────────────────────
        api.MapPost("/realtime/session", async (
            CreateSessionRequest request,
            [FromHeader(Name = "X-Family-Id")] Guid? headerFamilyId,
            [FromHeader(Name = "X-Child-Profile-Id")] Guid? headerChildId,
            [FromHeader(Name = "X-Device-Id")] string? deviceIdentifier,
            RealtimeSessionService sessionService,
            SessionManager sessionManager,
            SessionLeaseService leaseService,
            MemoryService memoryService,
            FamilyService familyService,
            IHostEnvironment env,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (!PersonaService.IsValidMode(request.Mode))
            {
                return Results.BadRequest(new { error = "Mode must be 'adult' or 'child'." });
            }

            var familyId = headerFamilyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var childProfileId = headerChildId ?? Guid.Parse("33333333-3333-3333-3333-333333333333");

            // Verify child belongs to family
            var child = await familyService.GetChildProfileAsync(familyId, childProfileId, ct);
            if (child is null && request.Mode == "child")
            {
                return Results.NotFound(new { error = "指定的儿童档案不存在。" });
            }

            // In child mode, enforce Session Lease
            var sessionId = Guid.NewGuid();
            if (request.Mode == "child")
            {
                var leaseResult = await leaseService.TryAcquireLeaseAsync(childProfileId, sessionId, null, ct);
                if (!leaseResult.Acquired)
                {
                    return Results.Conflict(new { error = leaseResult.RejectionReason });
                }
            }

            // Build dynamic memory instructions
            string memoryInstructions = string.Empty;
            if (request.Mode == "child")
            {
                memoryInstructions = await memoryService.BuildMemoryInstructionBlockAsync(familyId, childProfileId, ct);
            }

            if (!env.IsDevelopment())
            {
                request.MinimalConfig = false;
            }

            try
            {
                var sessionRes = await sessionService.CreateSessionAsync(request, ct);

                // Start persistent session in DB
                var dbSession = await sessionManager.StartSessionAsync(
                    familyId,
                    childProfileId,
                    null,
                    request.Mode,
                    sessionRes.AzureSessionId,
                    ct);

                return Results.Ok(new
                {
                    sessionRes.EphemeralKey,
                    sessionRes.ExpiresAt,
                    sessionRes.Endpoint,
                    sessionRes.MaxSessionSeconds,
                    ApplicationSessionId = dbSession.Id,
                    BusinessSessionId = sessionRes.BusinessSessionId,
                    MemoryInjected = !string.IsNullOrWhiteSpace(memoryInstructions)
                });
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                logger.LogError(ex, "Creating realtime session failed");
                if (request.Mode == "child")
                {
                    await leaseService.ReleaseLeaseAsync(childProfileId, sessionId, ct);
                }
                return Results.Problem(title: "Realtime session could not be created", statusCode: StatusCodes.Status502BadGateway);
            }
        }).RequireRateLimiting("session").WithTags("Realtime");

        // Session lifecycle endpoints
        var sessions = api.MapGroup("/sessions").WithTags("Sessions");

        sessions.MapPost("/{sessionId:guid}/checkpoint", async (
            Guid sessionId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            CheckpointRequest req,
            SessionManager sessionManager,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var ok = await sessionManager.CheckpointAsync(sessionId, fid, req, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        sessions.MapPost("/{sessionId:guid}/finalize", async (
            Guid sessionId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            FinalizeSessionRequest req,
            SessionManager sessionManager,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var res = await sessionManager.FinalizeSessionAsync(sessionId, fid, req, ct);
            return res.Success ? Results.Ok(res) : Results.NotFound();
        });

        sessions.MapGet("/{sessionId:guid}", async (
            Guid sessionId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            SessionManager sessionManager,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var session = await sessionManager.GetSessionAsync(sessionId, fid, ct);
            return session is not null ? Results.Ok(session) : Results.NotFound();
        });

        // ──────────────────────── 5. Memories & Digests ────────────────────────
        children.MapGet("/{childId:guid}/memories", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            MemoryService memoryService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var list = await memoryService.GetChildMemoriesAsync(fid, childId, ct);
            return Results.Ok(list);
        });

        children.MapPost("/{childId:guid}/memories", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            CreateMemoryRequest req,
            MemoryService memoryService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var memory = await memoryService.AddMemoryFactAsync(fid, childId, req, ct);
            return memory is not null ? Results.Ok(memory) : Results.NotFound();
        });

        children.MapDelete("/{childId:guid}/memories/{memoryId:guid}", async (
            Guid childId,
            Guid memoryId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            MemoryService memoryService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var ok = await memoryService.DeleteMemoryFactAsync(fid, childId, memoryId, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        children.MapGet("/{childId:guid}/digests", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            DiscoveryDigestService digestService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var digests = await digestService.GetChildDigestsAsync(fid, childId, ct);
            return Results.Ok(digests);
        });

        children.MapDelete("/{childId:guid}/digests/{digestId:guid}", async (
            Guid childId,
            Guid digestId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            DiscoveryDigestService digestService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var ok = await digestService.DeleteDigestAsync(fid, childId, digestId, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        // ──────────────────────── 6. Child Safety ────────────────────────
        children.MapGet("/{childId:guid}/safety-events", async (
            Guid childId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            SafetyAuditService safetyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var events = await safetyService.GetSafetyEventsAsync(fid, childId, ct);
            return Results.Ok(events);
        });

        children.MapPost("/{childId:guid}/safety-events/{eventId:guid}/ack", async (
            Guid childId,
            Guid eventId,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            SafetyAuditService safetyService,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var ok = await safetyService.AcknowledgeSafetyEventAsync(fid, childId, eventId, ct);
            return ok ? Results.Ok(new { acknowledged = true }) : Results.NotFound();
        });

        // ──────────────────────── 7. Capabilities & Tools ────────────────────────
        api.MapGet("/tools", (ToolRegistry registry) =>
        {
            var tools = registry.GetAllTools().Select(t => new
            {
                t.Name,
                t.Description,
                t.RequiredConsent
            });
            return Results.Ok(tools);
        }).WithTags("Capabilities");

        sessions.MapPost("/{sessionId:guid}/tools/{toolName}/execute", async (
            Guid sessionId,
            string toolName,
            [FromHeader(Name = "X-Family-Id")] Guid? familyId,
            [FromHeader(Name = "X-Child-Profile-Id")] Guid? childId,
            [FromBody] JsonElement args,
            ToolRegistry registry,
            CancellationToken ct) =>
        {
            var fid = familyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var cid = childId ?? Guid.Parse("33333333-3333-3333-3333-333333333333");
            var context = new BuddyToolContext(sessionId, cid, fid);
            var result = await registry.ExecuteToolAsync(toolName, context, args, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithTags("Capabilities");
    }
}
