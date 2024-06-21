// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class SliceFromTests
{
    [Theory]
    [InlineData("a", "a", "")]
    [InlineData("a", "z", "")]
    [InlineData("bbb", ":", "")]
    [InlineData("aaa.bbb", ".", "bbb")]
    [InlineData("aaa.bbb", "b", "bb")]
    [InlineData("aaa.bbb", "bbb", "")]
    [InlineData("aaa.bbb", "z", "")]
    [InlineData("aaa.bbb", "a", "aa.bbb")]
    [InlineData("aaa.bbb", "aaa", ".bbb")]
    [InlineData("abcdef.jpg", ".", "jpg")]
    [InlineData("abcdef.jpg.jpg", ".", "jpg.jpg")]
    public void From_All_Positions(string input, string delimiter, string expected)
    {
        input.SliceFrom(delimiter).ShouldBe(expected);
    }

    [Theory]
    [InlineData("abcdef.jpg", ".", "jpg")]
    [InlineData("abcdef.jpg", ".jpg", "")]
    [InlineData("abcdef.jpg.jpg", ".jpg", "")]
    [InlineData("jpg", ".", "")]
    [InlineData("abcdef.jpg.jpg", ".", "jpg")]
    public void FromLast_All_Positions(string input, string delimiter, string expected)
    {
        input.SliceFromLast(delimiter).ShouldBe(expected);
    }
}
