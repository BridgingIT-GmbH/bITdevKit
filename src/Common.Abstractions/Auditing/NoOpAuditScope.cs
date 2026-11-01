// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>A stateless handle that captures no metadata.</summary>
/// <example><code>IAuditScope scope = NoOpAuditScope.Instance;</code></example>
public sealed class NoOpAuditScope : IAuditScope
{
    /// <summary>The reusable stateless handle.</summary>
    /// <example><code>var scope = NoOpAuditScope.Instance;</code></example>
    public static NoOpAuditScope Instance { get; } = new();

    /// <summary>No execution was allocated.</summary>
    /// <example><code>var absent = scope.AuditId == Guid.Empty;</code></example>
    public Guid AuditId => Guid.Empty;

    /// <summary>No action was allocated.</summary>
    /// <example><code>var absent = scope.ActionId is null;</code></example>
    public Guid? ActionId => null;

    /// <summary>Always false.</summary>
    /// <example><code>var recording = scope.IsRecording;</code></example>
    public bool IsRecording => false;

    /// <summary>Ignores the supplied detail.</summary>
    /// <example><code>scope.SetDetail("count", AuditValue.From(1L));</code></example>
    public void SetDetail(string name, AuditValue value) { }

    /// <summary>Ignores the supplied resource.</summary>
    /// <example><code>scope.AddResource(resource);</code></example>
    public void AddResource(AuditResourceReference resource) { }

    /// <summary>Does not record an outcome.</summary>
    /// <example><code>scope.Complete();</code></example>
    public void Complete(AuditOutcome outcome = AuditOutcome.Succeeded) { }

    /// <summary>Does not retain the exception.</summary>
    /// <example><code>scope.Fail(error);</code></example>
    public void Fail(Exception exception) { }

    /// <summary>Completes synchronously without work.</summary>
    /// <example><code>scope.Dispose();</code></example>
    public void Dispose() { }

    /// <summary>Completes asynchronously without work.</summary>
    /// <example><code>await scope.DisposeAsync();</code></example>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
