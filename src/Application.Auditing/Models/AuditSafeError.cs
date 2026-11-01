// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Safe error classification without exception messages, stacks, or retained exception instances.</summary>
/// <example><code>var value = new AuditSafeError();</code></example>
public sealed record AuditSafeError
{
    /// <summary>Identity shared by observations of the same exception occurrence.</summary>
    /// <example><code>var value = snapshot.Id;</code></example>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Safe classification identifier.</summary>
    /// <example><code>var value = snapshot.Code;</code></example>
    public string Code { get; init; } = "Failure";

    /// <summary>Bounded exception type name; never the exception text.</summary>
    /// <example><code>var value = snapshot.ExceptionType;</code></example>
    public string ExceptionType { get; init; }

}
