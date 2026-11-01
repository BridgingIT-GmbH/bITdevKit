// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Forwards exception evidence directly into the live scope without retaining exception instances.</summary>
/// <example><code>feature.Observe(exception);</code></example>
public sealed class AuditHttpFeature(IAuditScope scope) : IAuditHttpErrorFeature
{
    /// <summary>Whether an exception was actually observed downstream.</summary>
    /// <example><code>var failed = feature.ExceptionObserved;</code></example>
    public bool ExceptionObserved { get; private set; }

    /// <summary>Whether a source explicitly identified an expected rejection.</summary>
    /// <example><code>var rejected = feature.KnownRejection;</code></example>
    public bool KnownRejection { get; private set; }

    /// <inheritdoc />
    public void Observe(Exception exception, bool knownRejection = false)
    {
        this.ExceptionObserved = true;
        this.KnownRejection |= knownRejection;
        scope.Fail(exception);
        if (exception is TimeoutException) scope.SetDetail("http.failureReason", AuditValue.From("Timeout"));
    }
}
