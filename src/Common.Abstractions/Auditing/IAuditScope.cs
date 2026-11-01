// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>A single owned execution or action scope. Recording roots require asynchronous disposal.</summary>
/// <example><code>await using var scope = audit.BeginScope(s => s.Key("customers.rename")); scope.Complete();</code></example>
public interface IAuditScope : IDisposable, IAsyncDisposable
{
    /// <summary>The owning execution identity.</summary>
    /// <example><code>var execution = scope.AuditId;</code></example>
    Guid AuditId { get; }

    /// <summary>The action occurrence, or null for a root.</summary>
    /// <example><code>var action = scope.ActionId;</code></example>
    Guid? ActionId { get; }

    /// <summary>Whether this handle currently accepts capture.</summary>
    /// <example><code>if (scope.IsRecording) scope.SetDetail("reason", AuditValue.From("Correction"));</code></example>
    bool IsRecording { get; }

    /// <summary>Captures an explicitly projected value under the configured policy.</summary>
    /// <example><code>scope.SetDetail("count", AuditValue.From(1L));</code></example>
    void SetDetail(string name, AuditValue value);

    /// <summary>Adds a typed business resource reference.</summary>
    /// <example><code>scope.AddResource&lt;Customer&gt;(customerId);</code></example>
    void AddResource(AuditResourceReference resource);

    /// <summary>Records the observed outcome without finalizing the owning root.</summary>
    /// <example><code>scope.Complete(AuditOutcome.Rejected);</code></example>
    void Complete(AuditOutcome outcome = AuditOutcome.Succeeded);

    /// <summary>Records a safe exception occurrence. Exception text is not captured automatically.</summary>
    /// <example><code>catch (Exception error) { scope.Fail(error); throw; }</code></example>
    void Fail(Exception exception);
}
