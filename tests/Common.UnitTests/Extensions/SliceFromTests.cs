// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class SliceFromTests
{
    [Fact]
    public void From_All_Positions()
    {
        Assert.Equal("a".SliceFrom("a"), string.Empty);
        Assert.Equal("a".SliceFrom("z"), string.Empty);
        Assert.Equal(string.Empty, "bbb".SliceFrom(":"));
        Assert.Equal("bbb", "aaa.bbb".SliceFrom("."));
        Assert.Equal("bb", "aaa.bbb".SliceFrom("b"));
        Assert.Equal(string.Empty, "aaa.bbb".SliceFrom("bbb"));
        Assert.Equal(string.Empty, "aaa.bbb".SliceFrom("z"));
        Assert.Equal("aa.bbb", "aaa.bbb".SliceFrom("a"));
        Assert.Equal(".bbb", "aaa.bbb".SliceFrom("aaa"));
    }

    [Fact]
    public void From_Last_Positions()
    {
        Assert.Equal("jpg", "abcdef.jpg".SliceFrom("."));
        Assert.Equal("jpg.jpg", "abcdef.jpg.jpg".SliceFrom("."));
        Assert.Equal("jpg", "abcdef.jpg".SliceFromLast("."));
        Assert.Equal(string.Empty, "jpg".SliceFromLast("."));
        Assert.Equal("jpg", "abcdef.jpg.jpg".SliceFromLast("."));
    }
}
