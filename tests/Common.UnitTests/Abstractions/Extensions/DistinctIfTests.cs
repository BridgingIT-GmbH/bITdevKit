// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class DistinctIfTests
{
    [Fact]
    public void DistinctIf_KeySelectorProvided_RetainsFirstItemForEachKey()
    {
        var source = new[]
        {
            new Item("first", 1),
            new Item("second", 1),
            new Item("third", 2)
        };

        var result = source.DistinctIf(item => item.Group).ToArray();

        result.ShouldBe([source[0], source[2]]);
    }

    [Fact]
    public void DistinctIf_NullKeySelector_ReturnsOriginalSequence()
    {
        IEnumerable<Item> source = [new Item("first", 1)];
        Func<Item, object> selector = null;

        var result = source.DistinctIf(selector);

        result.ShouldBeSameAs(source);
    }

    private sealed record Item(string Name, int Group);
}