// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain.Model;

public class EmailAddress : ValueObject
{
    private EmailAddress()
    {
    }

    private EmailAddress(string value)
    {
        this.Value = value;
    }

    public string Value { get; private set; }

    public static implicit operator string(EmailAddress email) => email.Value;

    public static EmailAddress Create(string value)
    {
        value = value?.Trim()?.ToLowerInvariant();

        DomainRules.Apply(
        [
            EmailAddressRules.IsValid(value),
        ]);

        return new EmailAddress(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}