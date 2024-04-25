// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class SliceTillTests
{
    [Fact]
    public void From_All_Positions()
    {
        Assert.Equal(string.Empty, "a".SliceTill("a"));
        Assert.Equal("aaa", "aaa".SliceTill(":"));
        Assert.Equal("aaa", "aaa.bbb".SliceTill("."));
        Assert.Equal("aaa.", "aaa.bbb".SliceTill("b"));
        Assert.Equal("aaa.", "aaa.bbb".SliceTill("bbb"));
        Assert.Equal("aaa.bbb", "aaa.bbb".SliceTill("z"));
        Assert.Equal(string.Empty, "aaa.bbb".SliceTill("a"));
        Assert.Equal(string.Empty, "aaa.bbb".SliceTill("aaa"));
    }

    [Fact]
    public void From_Last_Positions()
    {
        Assert.Equal("abcdef", "abcdef.jpg".SliceTill("."));
        Assert.Equal("abcdef", "abcdef.jpg.jpg".SliceTill("."));
        Assert.Equal("abcdef", "abcdef.jpg".SliceTillLast("."));
        Assert.Equal("abcdef.jpg", "abcdef.jpg.jpg".SliceTillLast("."));
    }
}