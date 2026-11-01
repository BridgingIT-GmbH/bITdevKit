// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.DataProtection;

/// <summary>Protects operation-bound deferred attribution with the host Data Protection key ring.</summary>
/// <remarks>Hosts sharing deferred work across restarts or nodes must persist and share the same appropriately protected key ring.</remarks>
/// <example><code>var link = protector.Unprotect(token, "Jobs", occurrenceId.ToString("D"));</code></example>
public sealed class AuditExecutionLinkProtector(IDataProtectionProvider provider) : IAuditExecutionLinkProtector
{
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 8, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <inheritdoc />
    public string Protect(AuditExecutionLink link, string feature, string operationId)
    {
        ValidateBinding(feature, operationId);
        if (!Valid(link)) throw new ArgumentException("A bounded trusted audit link is required.", nameof(link));
        var json = JsonSerializer.Serialize(link, Json);
        if (json.Length > 4_096) throw new ArgumentException("Audit handoff metadata exceeds the supported bound.", nameof(link));
        return this.Protector(feature, operationId).Protect(json);
    }

    /// <inheritdoc />
    public AuditExecutionLink Unprotect(string token, string feature, string operationId)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 16_384) return null;
        try
        {
            ValidateBinding(feature, operationId);
            var json = this.Protector(feature, operationId).Unprotect(token);
            if (json.Length > 4_096) return null;
            var link = JsonSerializer.Deserialize<AuditExecutionLink>(json, Json);
            return Valid(link) ? link : null;
        }
        catch (Exception) { return null; }
    }

    private IDataProtector Protector(string feature, string operationId) => provider.CreateProtector("BridgingIT.DevKit.Auditing.Handoff", "v1", feature, operationId);
    private static bool Text(string value, int maximum) => value is null || value.Length <= maximum && !value.Any(char.IsControl);
    private static bool Valid(AuditExecutionLink link) => link is not null && link.OriginAuditId != Guid.Empty && link.CausationAuditId != Guid.Empty && link.CausationActionId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(link.CorrelationId) && Text(link.CorrelationId, 128) && Text(link.InitiatingModuleName, 128) &&
        (link.InitiatingActor is null || Text(link.InitiatingActor.UserId, 1_024) && Text(link.InitiatingActor.DisplayName, 1_024) && link.InitiatingActor.Kind is "User" or "Service" or "Anonymous");
    private static void ValidateBinding(string feature, string identity)
    {
        if (string.IsNullOrWhiteSpace(feature) || !Text(feature, 64) || string.IsNullOrWhiteSpace(identity) || !Text(identity, 512))
            throw new ArgumentException("A bounded feature and operation identity are required.");
    }
}
