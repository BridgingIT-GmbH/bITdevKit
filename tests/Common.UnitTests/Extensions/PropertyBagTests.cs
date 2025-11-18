// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BridgingIT.DevKit.Common;
using Shouldly;
using Xunit;

public class PropertyBagTests
{
    [Fact]
    public void Set_And_Get_Value_By_String_Key()
    {
        var bag = new PropertyBag();
        bag.Set("foo", 42);
        bag.Get("foo").ShouldBe(42);
        bag.Get<int>("foo").ShouldBe(42);
    }

    [Fact]
    public void Get_NonExistent_Key_Returns_Null_Or_Default()
    {
        var bag = new PropertyBag();
        bag.Get("missing").ShouldBeNull();
        bag.Get<int>("missing").ShouldBe(0);
        bag.Get<string>("missing").ShouldBeNull();
    }

    [Fact]
    public void TryGet_Returns_True_And_Value_When_Key_Exists()
    {
        var bag = new PropertyBag();
        bag.Set("foo", 123);

        bag.TryGet<int>("foo", out var value).ShouldBeTrue();
        value.ShouldBe(123);
    }

    [Fact]
    public void TryGet_Returns_False_And_Default_When_Key_Missing()
    {
        var bag = new PropertyBag();
        bag.TryGet<int>("bar", out var value).ShouldBeFalse();
        value.ShouldBe(0);
    }

    [Fact]
    public void Remove_Removes_Key_And_Returns_True()
    {
        var bag = new PropertyBag();
        bag.Set("foo", 1);
        bag.Remove("foo").ShouldBeTrue();
        bag.Contains("foo").ShouldBeFalse();
    }

    [Fact]
    public void Remove_Returns_False_If_Key_Not_Found()
    {
        var bag = new PropertyBag();
        bag.Remove("notfound").ShouldBeFalse();
    }

    [Fact]
    public void RemoveAll_Removes_Keys_Matching_Predicate()
    {
        var bag = new PropertyBag();
        bag.Set("a", 1);
        bag.Set("b", 2);
        bag.Set("c", 3);
        bag.RemoveAll((k, v) => ((int)v) % 2 == 1);
        bag.Contains("a").ShouldBeFalse();
        bag.Contains("b").ShouldBeTrue();
        bag.Contains("c").ShouldBeFalse();
    }

    [Fact]
    public void Clear_Removes_All_Entries()
    {
        var bag = new PropertyBag();
        bag.Set("foo", 1);
        bag.Set("bar", 2);
        bag.Clear();
        bag.Keys.ShouldBeEmpty();
        bag.Values.ShouldBeEmpty();
    }

    [Fact]
    public void Indexer_Get_Set_Works()
    {
        var bag = new PropertyBag
        {
            ["foo"] = 100
        };
        bag["foo"].ShouldBe(100);
        bag["bar"].ShouldBeNull();
    }

    [Fact]
    public void Keys_And_Values_Enumerate_Correctly()
    {
        var bag = new PropertyBag();
        bag.Set("a", 1);
        bag.Set("b", 2);
        bag.Set("c", 3);

        bag.Keys.ShouldBe(["a", "b", "c"], ignoreOrder: true);
        bag.Values.ShouldBe([1, 2, 3], ignoreOrder: true);
    }

    [Fact]
    public void Clone_Produces_Deep_Copy()
    {
        var bag = new PropertyBag();
        bag.Set("x", 9);
        var clone = bag.Clone();
        clone.ShouldNotBeSameAs(bag);
        clone.Get("x").ShouldBe(9);

        // Change in original does not affect clone
        bag.Set("x", 10);
        clone.Get("x").ShouldBe(9);
    }

    [Fact]
    public void Merge_Overwrites_Existing_Keys()
    {
        var bag1 = new PropertyBag();
        bag1.Set("a", 1);
        bag1.Set("b", 2);

        var bag2 = new PropertyBag();
        bag2.Set("b", 20);
        bag2.Set("c", 30);

        bag1.Merge(bag2);

        bag1.Get("a").ShouldBe(1);
        bag1.Get("b").ShouldBe(20);
        bag1.Get("c").ShouldBe(30);
    }

    [Fact]
    public void Enumerator_Iterates_All_Entries()
    {
        var bag = new PropertyBag();
        bag.Set("foo", 1);
        bag.Set("bar", 2);
        var dict = bag.ToDictionary(kv => kv.Key, kv => kv.Value);
        dict.Count.ShouldBe(2);
        dict["foo"].ShouldBe(1);
        dict["bar"].ShouldBe(2);
    }

    [Fact]
    public void StronglyTypedKey_Set_And_Get()
    {
        var key = new PropertyBagKey<int>("counter");
        var bag = new PropertyBag();
        bag.Set(key, 42);
        bag.Get(key).ShouldBe(42);
        bag.Contains(key).ShouldBeTrue();
        bag.Remove(key).ShouldBeTrue();
        bag.Contains(key).ShouldBeFalse();
    }

    [Fact]
    public void Event_Is_Raised_On_ItemChanged()
    {
        var bag = new PropertyBag();
        string changedKey = null;
        object changedValue = null;
        bag.ItemChanged += (k, v) => { changedKey = k; changedValue = v; };
        bag.Set("foo", 5);
        changedKey.ShouldBe("foo");
        changedValue.ShouldBe(5);
    }

    [Fact]
    public void Thread_Safety_Set_And_Get()
    {
        var bag = new PropertyBag();
        const int threadCount = 10;
        const int iterations = 1000;
        var threads = new List<Thread>();

        for (var i = 0; i < threadCount; i++)
        {
            var n = i;
            threads.Add(new Thread(() =>
            {
                for (var j = 0; j < iterations; j++)
                {
                    bag.Set($"k{n}", j);
                }
            }));
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        for (var i = 0; i < threadCount; i++)
        {
            bag.Get<int>($"k{i}").ShouldBe(iterations - 1);
        }
    }
}