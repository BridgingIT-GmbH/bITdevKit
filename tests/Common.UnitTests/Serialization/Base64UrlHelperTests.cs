// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Text;

[UnitTest("Common")]
public class Base64UrlHelperTests
{
    [Theory]
    [InlineData("")]
    [InlineData("payload")]
    [InlineData("Base64Url with symbols: +/=")]
    public void EncodeDecode_WithBytes_RoundTripsCanonicalUnpaddedValue(string value)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(value);

        // Act
        var encoded = Base64UrlHelper.Encode(bytes);
        var decoded = Base64UrlHelper.Decode(encoded);

        // Assert
        decoded.ShouldBe(bytes);
        encoded.ShouldNotContain("+");
        encoded.ShouldNotContain("/");
        encoded.ShouldNotContain("=");
    }

    [Theory]
    [InlineData("cGF5bG9hZA==")]
    [InlineData("cGF5bG9hZB")]
    [InlineData("***")]
    public void Decode_WithNonCanonicalOrMalformedValue_ThrowsFormatException(string value)
    {
        // Act
        var action = () => Base64UrlHelper.Decode(value);

        // Assert
        action.ShouldThrow<FormatException>();
    }
}
