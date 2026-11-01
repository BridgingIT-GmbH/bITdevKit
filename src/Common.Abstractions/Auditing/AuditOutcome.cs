// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>The observed business result, independent of persistence delivery.</summary>
/// <example><code>scope.Complete(AuditOutcome.Succeeded);</code></example>
public enum AuditOutcome
{
    /// <summary>No explicit completion was observed.</summary>
    /// <example><code>var outcome = AuditOutcome.Unknown;</code></example>
    Unknown,

    /// <summary>Business work succeeded.</summary>
    /// <example><code>scope.Complete(AuditOutcome.Succeeded);</code></example>
    Succeeded,

    /// <summary>Business work failed.</summary>
    /// <example><code>scope.Complete(AuditOutcome.Failed);</code></example>
    Failed,

    /// <summary>Business admission or validation rejected the operation.</summary>
    /// <example><code>scope.Complete(AuditOutcome.Rejected);</code></example>
    Rejected,

    /// <summary>The operation observed cancellation.</summary>
    /// <example><code>scope.Complete(AuditOutcome.Canceled);</code></example>
    Canceled
}

/// <summary>Determines execution ownership.</summary>
/// <example><code>builder.Mode(AuditScopeMode.RequiresNew);</code></example>
public enum AuditScopeMode
{
    /// <summary>Join a live execution, otherwise start one.</summary>
    /// <example><code>builder.Mode(AuditScopeMode.JoinOrStart);</code></example>
    JoinOrStart,

    /// <summary>Own a separate execution linked to the current cause.</summary>
    /// <example><code>builder.Mode(AuditScopeMode.RequiresNew);</code></example>
    RequiresNew
}

/// <summary>Describes captured instrumentation, not application-wide coverage.</summary>
/// <example><code>var completeness = AuditCompleteness.Complete;</code></example>
public enum AuditCompleteness
{
    /// <summary>All admitted instrumentation finished without capture loss.</summary>
    /// <example><code>var complete = AuditCompleteness.Complete;</code></example>
    Complete,

    /// <summary>Capture limits omitted data.</summary>
    /// <example><code>var truncated = AuditCompleteness.Truncated;</code></example>
    Truncated,

    /// <summary>Scopes or observations did not complete.</summary>
    /// <example><code>var incomplete = AuditCompleteness.Incomplete;</code></example>
    Incomplete
}
