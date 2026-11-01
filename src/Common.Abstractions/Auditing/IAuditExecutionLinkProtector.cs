// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Authenticates a bounded causal snapshot for one deferred source operation, independently of record HMAC keys.</summary>
/// <example><code>var token = protector.Protect(link, "Jobs", occurrenceId.ToString("D"));</code></example>
public interface IAuditExecutionLinkProtector
{
    /// <summary>Protects a trusted live snapshot for one feature and exact operation identity.</summary>
    /// <example><code>var token = protector.Protect(link, "Messaging", messageId);</code></example>
    string Protect(AuditExecutionLink link, string feature, string operationId);

    /// <summary>Returns the authenticated snapshot, or null for missing, forged, or unavailable metadata.</summary>
    /// <example><code>var link = protector.Unprotect(token, "Messaging", messageId);</code></example>
    AuditExecutionLink Unprotect(string token, string feature, string operationId);
}
