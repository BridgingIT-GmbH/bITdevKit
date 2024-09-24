// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class SliceTillTests
{
    [Theory]
    [InlineData("a", "a", "")]
    [InlineData("aaa", ":", "aaa")]
    [InlineData("aaa.bbb", ".", "aaa")]
    [InlineData("aaa.bbb", "b", "aaa.")]
    [InlineData("aaa.bbb", "bbb", "aaa.")]
    [InlineData("aaa.bbb", "z", "aaa.bbb")]
    [InlineData("aaa.bbb", "a", "")]
    [InlineData("aaa.bbb", "aaa", "")]
    [InlineData("abcdef.jpg", ".", "abcdef")]
    [InlineData("abcdef.jpg.jpg", ".", "abcdef")]
    public void Till_All_Positions(string source, string till, string expected)
    {
        source.SliceTill(till)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData("abcdef.jpg", ".", "abcdef")]
    [InlineData("abcdef.jpg", ".jpg", "abcdef")]
    [InlineData("abcdef.jpgn", "n", "abcdef.jpg")]
    [InlineData("abcdef.jpg.jpg", ".", "abcdef.jpg")]
    public void TillLast_All_Positions(string source, string till, string expected)
    {
        source.SliceTillLast(till)
            .ShouldBe(expected);
    }
}