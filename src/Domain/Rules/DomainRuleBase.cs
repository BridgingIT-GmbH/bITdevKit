// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

public abstract class DomainRuleBase : IDomainRule
{
    public virtual string Message => "Rule not satisfied";

    public virtual Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public abstract Task<bool> ApplyAsync(CancellationToken cancellationToken = default);
    // TODO: maybe refactor and use Result with success/failure and optional messages/errors
}