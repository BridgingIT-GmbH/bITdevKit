// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>A safe auditing reason code. Messages never echo supplied records or secrets.</summary>
/// <example><code>var error = new AuditError("AuditIdentityConflict");</code></example>
public sealed class AuditError(string code) : ResultErrorBase(code)
{
    /// <summary>The stable machine-readable reason identifier.</summary>
    /// <example><code>var code = error.Code;</code></example>
    public string Code { get; } = code;
}
