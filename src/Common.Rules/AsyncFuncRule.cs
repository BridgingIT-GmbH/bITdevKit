// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class AsyncFuncRule(Func<CancellationToken, Task<bool>> predicate, string message = "Async predicate rule not satisfied")
    : AsyncRuleBase
{
    public override string Message { get; } = message;

    protected override async Task<Result> ExecuteAsync(CancellationToken cancellationToken) =>
        Result.SuccessIf(await predicate(cancellationToken));
}