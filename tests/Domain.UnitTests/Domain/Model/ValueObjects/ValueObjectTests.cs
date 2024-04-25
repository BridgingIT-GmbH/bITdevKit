// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
public class ValueObjectTests
{
    [Fact]
    public void EqualityTests()
    {
        var instance0 = new StubValueObject
        {
            StreetName = "One Microsoft Way",
            HouseNumber = 1,
            City = "Seattle"
        };

        var instance1 = new StubValueObject
        {
            StreetName = "One Microsoft Way",
            HouseNumber = 1,
            City = "Seattle"
        };

        var instance2 = new StubValueObject
        {
            StreetName = "One Microsoft Way",
            HouseNumber = 1,
            City = "New York"
        };

        instance0.Equals(instance1).ShouldBeTrue(); //IEquatable (equals)
        instance0.ShouldBe(instance1);
        (instance0 == instance1).ShouldBeTrue(); // operator
        instance1.ShouldBe(instance1);
#pragma warning disable CS1718 // Comparison made to same variable
        (instance1 == instance1).ShouldBeTrue(); // operator
#pragma warning restore CS1718 // Comparison made to same variable
        instance1.ShouldNotBe(instance2);
        instance0.Equals(instance2).ShouldBeFalse(); // IEquatable
    }

    [Fact]
    public void ComparableTests()
    {
        var instance0 = new StubValueObjectComparable
        {
            StreetName = "One Microsoft Way",
            HouseNumber = 1,
            City = "Seattle",
            ZipCode = 2222
        };

        var instance1 = new StubValueObjectComparable
        {
            StreetName = "One Microsoft Way",
            HouseNumber = 1,
            City = "Seattle",
            ZipCode = 3333
        };

        var instance2 = new StubValueObjectComparable
        {
            StreetName = "One Microsoft Way",
            HouseNumber = 1,
            City = "New York",
            ZipCode = 1111
        };

        var instance3 = new StubValueObjectComparable
        {
            StreetName = "Second Microsoft Way",
            HouseNumber = 10,
            City = "New York",
            ZipCode = 1111
        };

        instance0.Equals(instance1).ShouldBeTrue(); //IEquatable (equals), same address
        (instance0 == instance1).ShouldBeTrue(); //IEquatable (equals), operator, same address
        instance1.ShouldBe(instance1);
#pragma warning disable CS1718 // Comparison made to same variable
        (instance1 == instance1).ShouldBeTrue(); // operator
#pragma warning restore CS1718 // Comparison made to same variable
        instance1.ShouldNotBe(instance2);
        instance0.Equals(instance2).ShouldBeFalse(); // IEquatable

        var values = new List<StubValueObjectComparable>
        {
            instance0,
            instance1,
            instance2,
            instance3
        };
        values.Sort();
        values.First().ZipCode.ShouldBe(1111); // IComparable (compare),
        values.Skip(1).Take(1).First().ZipCode.ShouldBe(1111); // IComparable (compare),
        values.Skip(2).Take(1).First().ZipCode.ShouldBe(2222); // IComparable (compare),
        values.Last().ZipCode.ShouldBe(3333); // 3333

        (instance0 > instance2).ShouldBeTrue(); // operator
        (instance2 > instance1).ShouldBeFalse(); // operator
        (instance2 >= instance3).ShouldBeTrue(); // operator
        (instance2 <= instance3).ShouldBeTrue(); // operator
    }

    public class StubValueObject : ValueObject
    {
        public string StreetName { get; set; }

        public int HouseNumber { get; set; }

        public string City { get; set; }

        protected override IEnumerable<object> GetAtomicValues()
        {
            yield return this.StreetName;
            yield return this.HouseNumber;
            yield return this.City;
        }
    }

    public class StubValueObjectComparable : ComparableValueObject
    {
        public string StreetName { get; set; }

        public int HouseNumber { get; set; }

        public string City { get; set; }

        public int ZipCode { get; set; }

        protected override IEnumerable<object> GetAtomicValues()
        {
            yield return this.StreetName;
            yield return this.HouseNumber;
            yield return this.City;
        }

        protected override IEnumerable<IComparable> GetComparableAtomicValues()
        {
            yield return this.ZipCode;
        }
    }
}
