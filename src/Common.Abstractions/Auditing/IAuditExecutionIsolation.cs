// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Optional source-factory capability that isolates delivery without implicitly selecting an audit root.</summary>
/// <example><code>await isolation.RunIsolatedAsync("Messaging", messageId, token, correlationId, next);</code></example>
public interface IAuditExecutionIsolation
{
    /// <summary>Clears inherited capture state, preserving only authenticated causation and validated technical correlation.</summary>
    /// <example><code>return await isolation.RunIsolatedAsync("Queueing", messageId, null, correlationId, ProcessAsync);</code></example>
    Task<T> RunIsolatedAsync<T>(string feature, string operationId, string token, string correlationId, Func<Task<T>> next);
}
