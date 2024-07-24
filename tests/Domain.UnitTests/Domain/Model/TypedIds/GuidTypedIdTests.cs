// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
public class GuidTypedIdTests
{
    [Fact]
    public void ValueTests()
    {
        var instance0 = new StubTypedId(Guid.Parse("4c073f99-da76-43b3-84e9-c013ef47e6b2"));

        instance0.Value.ShouldBe(Guid.Parse("4c073f99-da76-43b3-84e9-c013ef47e6b2"));
    }

    [Fact]
    public void EqualityTests()
    {
        var instance0 = new StubTypedId(Guid.Parse("4c073f99-da76-43b3-84e9-c013ef47e6b2"));
        var instance1 = new StubTypedId(Guid.Parse("0f6f4bd0-7b30-4e8b-b0c9-f4d5c35cf6e3"));
        var instance2 = new StubTypedId(Guid.Parse("c5520c3a-4526-4b87-a44c-181442d824d6"));
        var instance3 = new StubTypedId(Guid.Parse("c5520c3a-4526-4b87-a44c-181442d824d6"));

        instance0.Equals(instance1).ShouldBeFalse(); //IEquatable (equals)
        //instance0.ShouldBe(instance0.Value);
        (instance0 == instance0.Value).ShouldBeTrue(); // operator
        (instance0 == instance1).ShouldBeFalse(); // operator
        instance1.ShouldBe(instance1);
#pragma warning disable CS1718 // Comparison made to same variable
        (instance1 == instance1).ShouldBeTrue(); // operator
#pragma warning restore CS1718 // Comparison made to same variable
        instance1.ShouldNotBe(instance2);
        instance0.Equals(instance2).ShouldBeFalse(); // IEquatable
        instance2.Equals(instance3).ShouldBeTrue(); // IEquatable
        (instance2 == instance3.Value).ShouldBeTrue(); // operator
    }

    [Fact]
    public void ComparableTests()
    {
        var instance0 = new StubTypedId(Guid.Parse("4c073f99-da76-43b3-84e9-c013ef47e6b2")); // 2
        var instance1 = new StubTypedId(Guid.Parse("0f6f4bd0-7b30-4e8b-b0c9-f4d5c35cf6e3")); // 1
        var instance2 = new StubTypedId(Guid.Parse("c5520c3a-4526-4b87-a44c-181442d824d6")); // 4
        var instance3 = new StubTypedId(Guid.Parse("4e909369-b858-4b2b-b1fb-64e8085d8b7f")); // 3

        var values = new List<StubTypedId>
        {
            instance0,
            instance1,
            instance2,
            instance3
        };
        values.Sort();
        values.First().ShouldBe(instance1); // IComparable (compare),
        values.Skip(1).Take(1).First().ShouldBe(instance0); // IComparable (compare),
        values.Skip(2).Take(1).First().ShouldBe(instance3); // IComparable (compare),
        values.Last().ShouldBe(instance2); // 3333

        //(instance0 > instance2).ShouldBeTrue(); // operator
        //(instance2 > instance1).ShouldBeFalse(); // operator
        //(instance2 >= instance3).ShouldBeTrue(); // operator
        //(instance2 <= instance3).ShouldBeTrue(); // operator
    }

    public class StubTypedId : GuidTypedId
    {
        public StubTypedId()
            : base(Guid.Empty)
        {
        }

        public StubTypedId(Guid value)
            : base(value)
        {
        }
    }
}
