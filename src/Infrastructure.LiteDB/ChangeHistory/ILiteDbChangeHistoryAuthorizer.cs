// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;

/// <summary>Evaluates configured host policies before history values are read or an entity is restored.</summary>
public interface ILiteDbChangeHistoryAuthorizer
{
    /// <summary>Authorizes the selected operation. A failure prevents reading values and executing restore logic.</summary>
    /// <param name="context">The entity, policy, and operation requested.</param>
    /// <param name="cancellationToken">Cancels policy evaluation.</param>
    /// <returns>The authorization decision.</returns>
    Task<Result> AuthorizeAsync(LiteDbChangeHistoryAuthorizationContext context, CancellationToken cancellationToken = default);
}

/// <summary>Identifies a protected history operation without exposing stored values.</summary>
/// <param name="EntityType">The entity CLR type.</param>
/// <param name="EntityId">The optional selected entity identity.</param>
/// <param name="Policy">The configured host authorization policy.</param>
/// <param name="Operation">Read or Restore.</param>
/// <param name="Reason">The optional restore reason.</param>
public sealed record LiteDbChangeHistoryAuthorizationContext(Type EntityType, string EntityId, string Policy, string Operation, string Reason);
