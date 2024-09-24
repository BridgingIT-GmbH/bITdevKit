// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using Common;
using DevKit.Domain;
using DevKit.Domain.Model;

public class AdAccount : ValueObject
{
    private AdAccount() // TODO: make private again when System.Text.Json can deserialize objects with a non-public ctor
    { }

    private AdAccount(string domain, string name)
    {
        this.Domain = domain;
        this.Name = name;
    }

    public string Domain { get; }

    public string Name { get; }

    public static AdAccount Create(string value)
    {
        EnsureArg.IsNotNullOrEmpty(value, nameof(value));

        DomainRules.Apply(new AdAccountShouldBePartOfDomain(value));

        return new AdAccount(value.SliceTill("\\"), value.SliceFrom("\\"));
    }

    public override string ToString()
    {
        return $"{this.Domain}\\{this.Name}";
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Domain;
        yield return this.Name;
    }
}