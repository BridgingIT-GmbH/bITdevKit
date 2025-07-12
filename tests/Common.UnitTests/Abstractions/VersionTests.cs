// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

using System.Reflection;
using Xunit;
using Shouldly;

public class VersionTests
{
    [Fact]
    public void Parse_ValidVersion_ReturnsCorrectVersion()
    {
        // Arrange
        const string versionString = "1.0.8-preview0.2+3d57ae10dfa788f109ca91a47ccc";

        // Act
        var version = Version.Parse(versionString);

        // Assert
        version.Major.ShouldBe(1);
        version.Minor.ShouldBe(0);
        version.Patch.ShouldBe(8);
        version.Prerelease.ShouldBe("preview0.2");
        version.BuildMetadata.ShouldBe("3d57ae10dfa788f109ca91a47ccc");
        version.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Parse_InvalidVersion_ReturnsDefaultVersion()
    {
        // Arrange
        const string versionString = "invalid";

        // Act
        var version = Version.Parse(versionString);

        // Assert
        version.Major.ShouldBe(0);
        version.Minor.ShouldBe(0);
        version.Patch.ShouldBe(0);
        version.Prerelease.ShouldBeNull();
        version.BuildMetadata.ShouldBeNull();
        version.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Parse_NullOrEmptyVersion_ReturnsDefaultVersion()
    {
        // Arrange
        const string versionString = null;

        // Act
        var version1 = Version.Parse(versionString);
        var version2 = Version.Parse("");

        // Assert
        version1.Major.ShouldBe(0);
        version1.IsValid.ShouldBeFalse();
        version2.Major.ShouldBe(0);
        version2.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ParseFromAssembly_ValidAssembly_ReturnsCorrectVersion()
    {
        // Arrange
        var assembly = new FakeAssembly("1.2.3-beta+build456");

        // Act
        var version = Version.Parse(assembly);

        // Assert
        version.Major.ShouldBe(1);
        version.Minor.ShouldBe(2);
        version.Patch.ShouldBe(3);
        version.Prerelease.ShouldBe("beta");
        version.BuildMetadata.ShouldBe("build456");
        version.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ParseFromAssembly_NullAssembly_ReturnsDefaultVersion()
    {
        // Act
        var version = Version.Parse((Assembly)null);

        // Assert
        version.Major.ShouldBe(0);
        version.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CompareTo_VersionsWithDifferentMajor_ReturnsCorrectOrder()
    {
        // Arrange
        var v1 = Version.Parse("1.0.0");
        var v2 = Version.Parse("2.0.0");

        // Act & Assert
        (v1 < v2).ShouldBeTrue();
        (v2 > v1).ShouldBeTrue();
        v1.CompareTo(v2).ShouldBeLessThan(0);
    }

    [Fact]
    public void CompareTo_VersionsWithPrerelease_ReturnsCorrectOrder()
    {
        // Arrange
        var v1 = Version.Parse("1.0.0-alpha");
        var v2 = Version.Parse("1.0.0");

        // Act & Assert
        (v1 < v2).ShouldBeTrue();
        (v2 > v1).ShouldBeTrue();
        v1.CompareTo(v2).ShouldBeLessThan(0);
    }

    [Fact]
    public void CompareTo_EqualVersions_ReturnsZero()
    {
        // Arrange
        var v1 = Version.Parse("1.0.0-alpha+build1");
        var v2 = Version.Parse("1.0.0-alpha+build2");

        // Act & Assert
        v1.CompareTo(v2).ShouldBe(0);
        (v1 == v2).ShouldBeTrue();
    }

    [Fact]
    public void ToString_SimpleFormat_ReturnsMajorMinorPatch()
    {
        // Arrange
        var version = Version.Parse("1.0.8-preview0.2+3d57ae10dfa788f109ca91a47ccc");

        // Act
        var result = version.ToString(VersionFormat.Short);

        // Assert
        result.ShouldBe("1.0.8");
    }

    [Fact]
    public void ToString_WithPrereleaseFormat_ReturnsVersionWithPrerelease()
    {
        // Arrange
        var version = Version.Parse("1.0.8-preview0.2+3d57ae10dfa788f109ca91a47ccc");

        // Act
        var result = version.ToString(VersionFormat.WithPrerelease);

        // Assert
        result.ShouldBe("1.0.8-preview0.2");
    }

    [Fact]
    public void ToString_FullFormat_ReturnsFullVersion()
    {
        // Arrange
        var version = Version.Parse("1.0.8-preview0.2+3d57ae10dfa788f109ca91a47ccc");

        // Act
        var result = version.ToString(VersionFormat.Full);

        // Assert
        result.ShouldBe("1.0.8-preview0.2+3d57ae10dfa788f109ca91a47ccc");
    }

    [Fact]
    public void ToString_Default_ReturnsFullFormat()
    {
        // Arrange
        var version = Version.Parse("1.0.8-preview0.2+3d57ae10dfa788f109ca91a47ccc");

        // Act
        var result = version.ToString();

        // Assert
        result.ShouldBe("1.0.8-preview0.2+3d57ae10dfa788f109ca91a47ccc");
    }

    [Fact]
    public void Equals_SameVersions_ReturnsTrue()
    {
        // Arrange
        var v1 = Version.Parse("1.0.0-alpha+build1");
        var v2 = Version.Parse("1.0.0-alpha+build2");

        // Act & Assert
        v1.Equals(v2).ShouldBeTrue();
        (v1 == v2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentVersions_ReturnsFalse()
    {
        // Arrange
        var v1 = Version.Parse("1.0.0-alpha");
        var v2 = Version.Parse("1.0.0-beta");

        // Act & Assert
        v1.Equals(v2).ShouldBeFalse();
        (v1 != v2).ShouldBeTrue();
    }
}

// Fake Assembly for testing
public class FakeAssembly : Assembly
{
    private readonly string _informationalVersion;

    public FakeAssembly(string informationalVersion)
    {
        this._informationalVersion = informationalVersion;
    }

    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        if (attributeType == typeof(AssemblyInformationalVersionAttribute))
        {
            return new[] { new AssemblyInformationalVersionAttribute(this._informationalVersion) };
        }
        return Array.Empty<object>();
    }
}