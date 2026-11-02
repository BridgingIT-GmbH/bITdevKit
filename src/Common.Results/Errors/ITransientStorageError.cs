// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Exposes whether a storage failure may be retried within the configured policy budget.</summary>
/// <remarks>Providers must report uncertain mutation outcomes separately. Such outcomes must take precedence over this marker and must not be replayed automatically.</remarks>
/// <example><code>var transient = result.Errors.Any(error =&gt; error is ITransientStorageError { IsTransient: true });</code></example>
public interface ITransientStorageError : IResultError
{
    /// <summary>Gets whether retrying this failure is permitted, subject to operation-specific replay restrictions.</summary>
    /// <example><code>var retry = error is ITransientStorageError { IsTransient: true };</code></example>
    bool IsTransient { get; }
}