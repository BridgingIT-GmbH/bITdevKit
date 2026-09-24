// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class SafeRemoveTests
{
    [Fact]
    public void SafeRemove_List_ReturnsWhetherItemWasRemoved()
    {
        IList<string> source = ["first"];

        source.SafeRemove("first").ShouldBeTrue();
        source.SafeRemove("missing").ShouldBeFalse();
        source.ShouldBeEmpty();
    }

    [Fact]
    public void SafeRemove_Collection_ReturnsWhetherItemWasRemoved()
    {
        ICollection<string> source = new HashSet<string> { "first" };

        source.SafeRemove("first").ShouldBeTrue();
        source.SafeRemove("missing").ShouldBeFalse();
    }

    [Fact]
    public void SafeRemove_NullCollectionOrItem_ReturnsFalse()
    {
        ICollection<string> nullSource = null;
        ICollection<string> source = ["first"];

        nullSource.SafeRemove("first").ShouldBeFalse();
        source.SafeRemove(null).ShouldBeFalse();
    }

    [Fact]
    public void SafeRemove_Dictionary_RemovesKeyAndReturnsSource()
    {
        IDictionary<string, int> source = new Dictionary<string, int> { ["first"] = 1 };

        var result = source.SafeRemove("first");

        result.ShouldBeSameAs(source);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SafeRemove_NullDictionary_ReturnsNull()
    {
        IDictionary<string, int> source = null;

        source.SafeRemove("first").ShouldBeNull();
    }
}