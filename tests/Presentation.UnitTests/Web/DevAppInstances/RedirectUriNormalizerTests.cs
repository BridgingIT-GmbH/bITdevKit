// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.DevAppInstances;

using BridgingIT.DevKit.Presentation.Web;

[UnitTest("Presentation")]
public class RedirectUriNormalizerTests
{
    [Theory]
    [InlineData("/authentication/login-callback", true)]
    [InlineData("//evil.example/callback", false)]
    [InlineData("https://localhost:5111/callback", false)]
    [InlineData("", false)]
    public void IsRelative_VariousUris_ReturnsExpected(string uri, bool expected)
    {
        // Act
        var result = RedirectUriNormalizer.IsRelative(uri);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_RelativeUri_CombinesWithIssuerOrigin()
    {
        // Act
        var result = RedirectUriNormalizer.Resolve("/authentication/login-callback", "https://localhost:5111/");

        // Assert
        result.ShouldBe("https://localhost:5111/authentication/login-callback");
    }

    [Fact]
    public void Resolve_AbsoluteUri_ReturnsUnchanged()
    {
        // Act
        var result = RedirectUriNormalizer.Resolve("https://app.example/callback", "https://localhost:5111");

        // Assert
        result.ShouldBe("https://app.example/callback");
    }

    [Fact]
    public void Matches_RelativeRegistrationAndAbsoluteRequest_ReturnsTrue()
    {
        // Act
        var result = RedirectUriNormalizer.Matches(
            ["/authentication/login-callback"],
            "https://localhost:5111/authentication/login-callback",
            "https://localhost:5111");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Matches_RelativeRegistrationAndOtherOrigin_ReturnsFalse()
    {
        // Act
        var result = RedirectUriNormalizer.Matches(
            ["/authentication/login-callback"],
            "https://localhost:5211/authentication/login-callback",
            "https://localhost:5111");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_ProtocolRelativeUri_IsNotResolvedAgainstIssuer()
    {
        // Act
        var result = RedirectUriNormalizer.Resolve("//evil.example/callback", "https://localhost:5111");

        // Assert
        result.ShouldBe("//evil.example/callback");
    }

    [Theory]
    [InlineData("https://localhost:5111", true)]
    [InlineData("https://127.0.0.1:5111", true)]
    [InlineData("https://app.example", false)]
    [InlineData("/authentication/login-callback", false)]
    public void IsLoopback_VariousUris_ReturnsExpected(string uri, bool expected)
    {
        // Act
        var result = RedirectUriNormalizer.IsLoopback(uri);

        // Assert
        result.ShouldBe(expected);
    }
}
