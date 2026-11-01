// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Copies only protected causal metadata into an existing source-owned transport property.</summary>
/// <example><code>AuditHandoffMetadata.Stamp(services, scope, properties, "Messaging", messageId);</code></example>
public static class AuditHandoffMetadata
{
    /// <summary>The reserved protected transport property; its contents alone never establish trust.</summary>
    /// <example><code>properties.Remove(AuditHandoffMetadata.PropertyName);</code></example>
    public const string PropertyName = "__bdk.audit.handoff.v1";

    /// <summary>Reads a bounded opaque token without interpreting ordinary actor or causation properties.</summary>
    /// <example><code>var token = AuditHandoffMetadata.Read(properties);</code></example>
    public static string Read(IDictionary<string, object> properties)
    {
        if (properties?.TryGetValue(PropertyName, out var value) != true) return null;
        return ReadToken(value);
    }

    /// <summary>Reads a scalar token restored from an existing string or JSON-valued property bag.</summary>
    /// <example><code>var token = AuditHandoffMetadata.ReadToken(occurrence.Properties.Get(AuditHandoffMetadata.PropertyName));</code></example>
    public static string ReadToken(object value)
    {
        var token = value switch { string text => text, JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(), _ => null };
        return token?.Length <= 16_384 ? token : null;
    }

    /// <summary>Copies the current selected execution's protected cause before scheduling independent work.</summary>
    /// <remarks>Unavailable or suppressed capture emits no token and invokes no business metadata projection.</remarks>
    /// <example><code>var token = AuditHandoffMetadata.Capture(scopes, "Pipelines", executionId);</code></example>
    public static string Capture(IServiceScopeFactory scopes, string feature, string operationId)
    {
        IAuditCaptureDiagnostics diagnostics = null;
        try
        {
            using var services = scopes.CreateScope();
            var audit = services.ServiceProvider.GetService<IAuditContext>();
            diagnostics = audit as IAuditCaptureDiagnostics;
            if (audit?.IsEnabled != true || audit.Current is not { IsRecording: true } owner) return null;
            var link = (owner as IAuditSourceScope)?.CaptureLink();
            var protector = services.ServiceProvider.GetService<IAuditExecutionLinkProtector>();
            return link is null || protector is null ? null : protector.Protect(link, feature, operationId);
        }
        catch (Exception)
        {
            try { diagnostics?.ReportFailure("AuditHandoffProtectionUnavailable"); }
            catch (Exception) { /* Capture failures do not prevent source scheduling. */ }

            return null;
        }
    }

    /// <summary>Preserves an authenticated retry link or stamps the current selected source occurrence before acceptance.</summary>
    /// <remarks>A non-recording source clears stale metadata. This never propagates suppression or a live principal.</remarks>
    /// <example><code>AuditHandoffMetadata.Stamp(services, scope, message.Properties, "Messaging", message.MessageId);</code></example>
    public static void Stamp(IServiceProvider services, IAuditScope scope, IDictionary<string, object> properties, string feature, string operationId)
    {
        if (properties is null) return;
        if (scope?.IsRecording != true) { properties.Remove(PropertyName); return; }

        var protector = services.GetService<IAuditExecutionLinkProtector>();
        if (protector is null) { properties.Remove(PropertyName); (scope as IAuditCaptureDiagnostics)?.ReportFailure("AuditHandoffProtectionUnavailable"); return; }

        var existing = Read(properties);
        if (existing is not null && protector.Unprotect(existing, feature, operationId) is not null) return;
        var link = (scope as IAuditSourceScope)?.CaptureLink();
        if (link is null) { properties.Remove(PropertyName); return; }

        properties[PropertyName] = protector.Protect(link, feature, operationId);
    }
}
