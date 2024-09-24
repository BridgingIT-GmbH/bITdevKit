// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using DevKit.Domain.Model;

public class MenuReviewId : AggregateRootId<Guid>
{
    private MenuReviewId() { }

    private MenuReviewId(Guid guid)
    {
        this.Value = guid;
    }

    public override Guid Value { get; protected set; }

    public static MenuReviewId Create()
    {
        return new MenuReviewId(Guid.NewGuid());
    }

    public static MenuReviewId Create(Guid value)
    {
        return new MenuReviewId(value);
    }

    public static MenuReviewId Create(string value)
    {
        return new MenuReviewId(Guid.Parse(value));
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}