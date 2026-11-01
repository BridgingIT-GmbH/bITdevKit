// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Applies trusted source verification independently of editable document flags or response projection.</summary>
/// <example><code>var status = await policy.VerifyOriginalAsync(original);</code></example>
public sealed class AuditIntegrityReadPolicy(IAuditIntegrityVerifier verifier = null)
{
    /// <summary>Whether the host selected required integrity processing, including explicit unsigned-history policy.</summary>
    /// <example><code>var required = policy.IsConfigured;</code></example>
    public bool IsConfigured => verifier is not null;

    /// <summary>Returns a safe source status or a typed failure before exposing business values.</summary>
    /// <example><code>var result = await policy.VerifyOriginalAsync(original, cancellationToken);</code></example>
    public Task<Result<string>> VerifyOriginalAsync(AuditRecord original, CancellationToken cancellationToken = default) => verifier is not null
        ? verifier.VerifyAsync(original, cancellationToken)
        : Task.FromResult(original.IntegrityProtection is null ? Result<string>.Success("NotProtected") : Result<string>.Failure(new AuditError("AuditIntegrityUnavailable")));
}
