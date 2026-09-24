// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class ListExtensionsTests
{
    [Fact]
    public void AddOrUpdate_ListWithExistingItem_MovesItemToEnd()
    {
        IList<string> source = ["first", "second"];

        source.AddOrUpdate("first");

        source.ShouldBe(["second", "first"]);
    }

    [Fact]
    public void AddOrUpdate_Collection_AddsNewAndDoesNotDuplicateExistingItem()
    {
        ICollection<string> source = new HashSet<string> { "first" };

        source.AddOrUpdate("first");
        source.AddOrUpdate("second");

        source.ShouldBe(["first", "second"], ignoreOrder: true);
    }

    [Fact]
    public void AddOrUpdate_NullSourceOrItem_DoesNotThrow()
    {
        IList<string> nullSource = null;
        IList<string> source = ["first"];

        Should.NotThrow(() => nullSource.AddOrUpdate("first"));
        Should.NotThrow(() => source.AddOrUpdate(null));
        source.ShouldBe(["first"]);
    }
}