// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;
public interface IDomainRule
{
    string Message { get; }

    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    Task<bool> ApplyAsync(CancellationToken cancellationToken = default);
    // TODO: maybe refactor and use Result with success/failure and optional messages/errors
}

[Obsolete("Use IDomainRule from now on (incl IsSatisfiedAsync -> ApplyAsync)")]
public interface IBusinessRule : IDomainRule
{
    Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default);
}