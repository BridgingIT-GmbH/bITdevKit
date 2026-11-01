// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Authenticates complete original documents before any read projection.</summary>
/// <example><code>var verified = await verifier.VerifyAsync(original, cancellationToken);</code></example>
public interface IAuditIntegrityVerifier
{
    /// <summary>Returns Verified or explicitly permitted NotProtected, or a safe failure without business values.</summary>
    /// <example><code>var status = await verifier.VerifyAsync(original);</code></example>
    Task<Result<string>> VerifyAsync(AuditRecord original, CancellationToken cancellationToken = default);
}
