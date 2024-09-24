// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

using DevKit.Domain;
using DevKit.Domain.Model;

public class EmailAddress : ValueObject
{
    private EmailAddress() { }

    private EmailAddress(string value)
    {
        this.Value = value;
    }

    public string Value { get; }

    public static implicit operator string(EmailAddress email)
    {
        return email.Value;
    }

    public static EmailAddress Create(string value)
    {
        value = value?.Trim()?.ToLowerInvariant();

        DomainRules.Apply([new IsValidEmailAddressRule(value)]);

        return new EmailAddress(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}