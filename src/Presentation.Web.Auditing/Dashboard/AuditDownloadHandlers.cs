// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Server-issued, principal/scope-bound native form downloads. No export file or browser Blob is created.</summary>
/// <example><code>var ticket = await AuditDownloadHandlers.IssueAsync(http);</code></example>
public static class AuditDownloadHandlers
{
    /// <summary>Issues a short-lived download identity under current trusted scope, without opening an extraction or reserving a stream.</summary>
    /// <example><code>var ticket = await AuditDownloadHandlers.IssueAsync(http);</code></example>
    public static async Task<AuditDownloadTicket> IssueAsync(HttpContext context)
    {
        var plan = await context.RequestServices.GetRequiredService<IAuditQueryService>().PrepareAsync(null, context.RequestAborted).ConfigureAwait(false);
        if (plan.IsFailure) throw new AuditExportException("AuditReadScopeUnavailable");
        var id = Guid.NewGuid();
        var lifetime = context.RequestServices.GetRequiredService<AuditExportOptions>().MetadataLifetime;
        var value = new Ticket(id, context.RequestServices.GetRequiredService<AuditRuntime>().InstanceId, AuditExportTokenProtector.PrincipalFingerprint(context.User), plan.Value.ScopeFingerprint, context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().Add(lifetime < TimeSpan.FromMinutes(10) ? lifetime : TimeSpan.FromMinutes(10)));
        var protectedValue = Protector(context).Protect(JsonSerializer.Serialize(value));
        if (protectedValue.Length > 4096) throw new AuditExportException("AuditExportTokenTooLarge");
        return new(id, protectedValue);
    }

    /// <summary>Bounds form input and validates the ticket before invoking the same application export service as public routes.</summary>
    /// <example><code>return await AuditDownloadHandlers.DownloadAsync(http, runtime);</code></example>
    public static async Task<IResult> DownloadAsync(HttpContext context, AuditWebRuntime runtime)
    {
        if (!context.Request.HasFormContentType || context.Request.ContentType?.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) != true || context.Request.ContentLength > 32_768) return AuditHttpHandlers.Error("AuditDownloadInvalid");
        using var body = new MemoryStream(); var buffer = new byte[4096];
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) > 0)
        {
            if (body.Length + read > 32_768) return AuditHttpHandlers.Error("AuditDownloadInvalid");
            body.Write(buffer, 0, read);
        }

        body.Position = 0;
        using var reader = new FormReader(body) { KeyLengthLimit = 128, ValueLengthLimit = 16_384, ValueCountLimit = 8 };
        var form = await reader.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        var protectedValue = form.GetValueOrDefault("ticket").ToString();
        if (protectedValue.Length is < 1 or > 4096) return AuditHttpHandlers.Error("AuditDownloadInvalid");
        Ticket ticket;
        try { ticket = JsonSerializer.Deserialize<Ticket>(Protector(context).Unprotect(protectedValue)); }
        catch (Exception) { return AuditHttpHandlers.Error("AuditDownloadInvalid"); }

        if (ticket is null || ticket.ExportId == Guid.Empty || ticket.InstanceId != context.RequestServices.GetRequiredService<AuditRuntime>().InstanceId || ticket.ExpiresUtc <= context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow() || ticket.Principal != AuditExportTokenProtector.PrincipalFingerprint(context.User)) return AuditHttpHandlers.Error("AuditDownloadInvalid");
        var plan = await context.RequestServices.GetRequiredService<IAuditQueryService>().PrepareAsync(null, context.RequestAborted).ConfigureAwait(false);
        if (plan.IsFailure || plan.Value.ScopeFingerprint != ticket.Scope) return AuditHttpHandlers.Error("AuditDownloadInvalid");
        var json = form.GetValueOrDefault("filter").ToString();
        var parsed = string.IsNullOrWhiteSpace(json) ? Result<FilterModel>.Success(new()) : AuditFilterJson.Parse(System.Text.Encoding.UTF8.GetBytes(json));
        if (parsed.IsFailure) return AuditHttpHandlers.Error("AuditFilterInvalid");
        var filter = parsed.Value;
        context.Request.QueryString = QueryString.Create(new Dictionary<string, string>
        {
            ["format"] = form.GetValueOrDefault("format").FirstOrDefault() ?? "ndjson", ["continuationToken"] = form.GetValueOrDefault("continuationToken").ToString(), ["checkpoint"] = form.GetValueOrDefault("checkpoint").ToString()
        });
        return await AuditHttpHandlers.ExportAsync(context, filter, runtime, ticket.ExportId).ConfigureAwait(false);
    }

    private static IDataProtector Protector(HttpContext context) => context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("BridgingIT.DevKit.Auditing.DashboardDownload.v1");
    private sealed record Ticket(Guid ExportId, Guid InstanceId, string Principal, string Scope, DateTimeOffset ExpiresUtc);
}

/// <summary>Bounded form admission ticket and its server-issued companion identity.</summary>
/// <param name="ExportId">Identity of the future attempt.</param>
/// <param name="Ticket">Protected one-attempt admission.</param>
/// <example><code>var exportId = ticket.ExportId;</code></example>
public sealed record AuditDownloadTicket(Guid ExportId, string Ticket);
