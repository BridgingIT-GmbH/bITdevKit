// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

using DevKit.Domain.Model;

public class CustomerId : AggregateRootId<Guid>
{
    private CustomerId() { }

    private CustomerId(Guid guid)
    {
        this.Value = guid;
    }

    public override Guid Value { get; protected set; }

    public static CustomerId CreateUnique()
    {
        return new CustomerId(Guid.NewGuid());
    }

    public static CustomerId Create(Guid value)
    {
        return new CustomerId(value);
    }

    public static CustomerId Create(string value)
    {
        return new CustomerId(Guid.Parse(value));
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}