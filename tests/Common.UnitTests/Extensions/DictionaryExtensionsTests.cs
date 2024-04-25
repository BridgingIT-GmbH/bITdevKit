// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System.Collections.Generic;

[UnitTest("Common")]
public class DictionaryExtensionsTests
{
    [Fact]
    public void AddOrUpdate_AddKeysAndValues()
    {
        var sut = new Dictionary<string, object>();

        sut.AddOrUpdate("key1", "val1");
        sut.AddOrUpdate("key1", "val2");

        Assert.True(sut.ContainsKey("key1"));
        Assert.True(sut["key1"].Equals("val2"));
    }

    [Fact]
    public void AddOrUpdate_Add()
    {
        var sut = new Dictionary<string, object>();
        sut.AddOrUpdate("key1", "val1");
        sut.AddOrUpdate("key1", "val2");

        var items = new Dictionary<string, object>();
        items.AddOrUpdate("key3", "val4");
        items.AddOrUpdate("key4", "val4");

        sut.AddOrUpdate(items);

        Assert.True(sut.ContainsKey("key4"));
        Assert.True(sut["key4"].Equals("val4"));
    }
}
