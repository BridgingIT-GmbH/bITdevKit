// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Optional source-adapter capability implemented by recording scopes.</summary>
/// <example><code>(scope as IAuditSourceScope)?.SetSource(new() { Feature = "Requester" });</code></example>
public interface IAuditSourceScope
{
    /// <summary>Copies bounded approved scalar metadata into the current occurrence.</summary>
    /// <example><code>source.SetSource(new() { HandlerType = typeof(RenameCustomerHandler).FullName });</code></example>
    void SetSource(AuditSourceMetadata metadata);

    /// <summary>Snapshots trusted causation and initiating context for an actual independent execution.</summary>
    /// <example><code>var link = (audit.Current as IAuditSourceScope)?.CaptureLink();</code></example>
    AuditExecutionLink CaptureLink();
}
