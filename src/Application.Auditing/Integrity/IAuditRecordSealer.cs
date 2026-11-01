// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Optional asynchronous sealing of complete finalized content before provider admission.</summary>
/// <example><code>var sealedRecord = await sealer.SealAsync(record, cancellationToken);</code></example>
public interface IAuditRecordSealer
{
    /// <summary>Returns a protected frozen document or a typed failure without unsigned fallback.</summary>
    /// <example><code>var result = await sealer.SealAsync(record);</code></example>
    Task<Result<AuditRecord>> SealAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
