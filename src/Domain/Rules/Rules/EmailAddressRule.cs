// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

public class EmailAddressRule(string email) : IDomainRule
{
    public string Message => "Email must be in a valid format";

    public Result Apply()
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure()
                .WithError(new DomainRuleError(nameof(EmailAddressRule), "Email is required"));
        }

        // TODO: match against regex

        if (!email.Contains('@'))
        {
            return Result.Failure()
                .WithError(new DomainRuleError(nameof(EmailAddressRule), "Email must contain @"));
        }

        return Result.Success();
    }

    public Task<Result> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.Apply());
    }
}