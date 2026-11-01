// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Local Requester capture selection; commands are selected by convention and business queries explicitly.</summary>
/// <example><code>requester.WithAuditing(options => options with { Select = type => type == typeof(FindCustomerQuery) });</code></example>
public sealed record RequesterAuditOptions
{
    /// <summary>Optional complete selection override; omission selects only resolved Command kinds.</summary>
    /// <example><code>var options = new RequesterAuditOptions { Select = type => type.Name.Split('`')[0].EndsWith("Command") };</code></example>
    public Func<Type, bool> Select { get; init; }

    /// <summary>Resolves Command, Query, or Request without serializing or evaluating the request instance.</summary>
    /// <example><code>var kind = options.ResolveKind(typeof(RenameCustomerCommand));</code></example>
    public Func<Type, string> ResolveKind { get; init; } = type => type.Name.Split('`')[0].EndsWith("Command", StringComparison.Ordinal) ? "Command" : type.Name.Split('`')[0].EndsWith("Query", StringComparison.Ordinal) ? "Query" : "Request";
}
