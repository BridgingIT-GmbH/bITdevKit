// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>Selects meaningful repository reads after explicit local audit behavior registration.</summary>
/// <example><code>repository.WithAuditing(new() { SelectRead = operation => operation == "FindOne" });</code></example>
public sealed record RepositoryAuditOptions
{
    /// <summary>Optional read selection; mutations participate by default and reads are omitted.</summary>
    /// <example><code>var selected = options.SelectRead?.Invoke("FindOne") == true;</code></example>
    public Func<string, bool> SelectRead { get; init; }
}
