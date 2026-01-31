// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.DataPorter;

using BridgingIT.DevKit.Common.DataPorter;

[UnitTest("Common")]
public class ProfileRegistryTests
{
    [Fact]
    public void Constructor_WithNullProfiles_CreatesEmptyRegistry()
    {
        // Arrange & Act
        var sut = new ProfileRegistry();

        // Assert
        sut.GetAllExportProfiles().ShouldBeEmpty();
        sut.GetAllImportProfiles().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithExportProfiles_RegistersProfiles()
    {
        // Arrange
        var exportProfile = new TestExportProfile();
        var exportProfiles = new[] { exportProfile };

        // Act
        var sut = new ProfileRegistry(exportProfiles);

        // Assert
        sut.GetAllExportProfiles().Count.ShouldBe(1);
    }

    [Fact]
    public void Constructor_WithImportProfiles_RegistersProfiles()
    {
        // Arrange
        var importProfile = new TestImportProfile();
        var importProfiles = new[] { importProfile };

        // Act
        var sut = new ProfileRegistry(importProfiles: importProfiles);

        // Assert
        sut.GetAllImportProfiles().Count.ShouldBe(1);
    }

    [Fact]
    public void RegisterExportProfile_WithValidProfile_AddsProfile()
    {
        // Arrange
        var sut = new ProfileRegistry();
        var profile = new TestExportProfile();

        // Act
        sut.RegisterExportProfile(profile);

        // Assert
        sut.GetAllExportProfiles().Count.ShouldBe(1);
    }

    [Fact]
    public void RegisterExportProfile_WithNullProfile_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new ProfileRegistry();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.RegisterExportProfile(null));
    }

    [Fact]
    public void RegisterImportProfile_WithValidProfile_AddsProfile()
    {
        // Arrange
        var sut = new ProfileRegistry();
        var profile = new TestImportProfile();

        // Act
        sut.RegisterImportProfile(profile);

        // Assert
        sut.GetAllImportProfiles().Count.ShouldBe(1);
    }

    [Fact]
    public void RegisterImportProfile_WithNullProfile_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new ProfileRegistry();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.RegisterImportProfile(null));
    }

    [Fact]
    public void GetExportProfile_WithExistingProfile_ReturnsProfile()
    {
        // Arrange
        var profile = new TestExportProfile();
        var sut = new ProfileRegistry([profile]);

        // Act
        var result = sut.GetExportProfile<TestExportEntity>();

        // Assert
        result.ShouldNotBeNull();
        result.SourceType.ShouldBe(typeof(TestExportEntity));
    }

    [Fact]
    public void GetExportProfile_WithNonExistingProfile_ReturnsNull()
    {
        // Arrange
        var sut = new ProfileRegistry();

        // Act
        var result = sut.GetExportProfile<TestExportEntity>();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetExportProfile_ByType_WithExistingProfile_ReturnsProfile()
    {
        // Arrange
        var profile = new TestExportProfile();
        var sut = new ProfileRegistry([profile]);

        // Act
        var result = sut.GetExportProfile(typeof(TestExportEntity));

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void GetExportProfile_ByType_WithNullType_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new ProfileRegistry();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.GetExportProfile(null));
    }

    [Fact]
    public void GetImportProfile_WithExistingProfile_ReturnsProfile()
    {
        // Arrange
        var profile = new TestImportProfile();
        var sut = new ProfileRegistry(importProfiles: [profile]);

        // Act
        var result = sut.GetImportProfile<TestImportEntity>();

        // Assert
        result.ShouldNotBeNull();
        result.TargetType.ShouldBe(typeof(TestImportEntity));
    }

    [Fact]
    public void GetImportProfile_WithNonExistingProfile_ReturnsNull()
    {
        // Arrange
        var sut = new ProfileRegistry();

        // Act
        var result = sut.GetImportProfile<TestImportEntity>();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetImportProfile_ByType_WithNullType_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new ProfileRegistry();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.GetImportProfile(null));
    }

    [Fact]
    public void TryGetExportProfile_WithExistingProfile_ReturnsTrueAndProfile()
    {
        // Arrange
        var profile = new TestExportProfile();
        var sut = new ProfileRegistry([profile]);

        // Act
        var success = sut.TryGetExportProfile<TestExportEntity>(out var result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBeNull();
    }

    [Fact]
    public void TryGetExportProfile_WithNonExistingProfile_ReturnsFalseAndNull()
    {
        // Arrange
        var sut = new ProfileRegistry();

        // Act
        var success = sut.TryGetExportProfile<TestExportEntity>(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void TryGetImportProfile_WithExistingProfile_ReturnsTrueAndProfile()
    {
        // Arrange
        var profile = new TestImportProfile();
        var sut = new ProfileRegistry(importProfiles: [profile]);

        // Act
        var success = sut.TryGetImportProfile<TestImportEntity>(out var result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBeNull();
    }

    [Fact]
    public void TryGetImportProfile_WithNonExistingProfile_ReturnsFalseAndNull()
    {
        // Arrange
        var sut = new ProfileRegistry();

        // Act
        var success = sut.TryGetImportProfile<TestImportEntity>(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void RegisterExportProfile_WithDuplicateType_OverwritesExistingProfile()
    {
        // Arrange
        var sut = new ProfileRegistry();
        var profile1 = new TestExportProfile();
        var profile2 = new TestExportProfile();

        // Act
        sut.RegisterExportProfile(profile1);
        sut.RegisterExportProfile(profile2);

        // Assert
        sut.GetAllExportProfiles().Count.ShouldBe(1);
    }

    [Fact]
    public void RegisterImportProfile_WithDuplicateType_OverwritesExistingProfile()
    {
        // Arrange
        var sut = new ProfileRegistry();
        var profile1 = new TestImportProfile();
        var profile2 = new TestImportProfile();

        // Act
        sut.RegisterImportProfile(profile1);
        sut.RegisterImportProfile(profile2);

        // Assert
        sut.GetAllImportProfiles().Count.ShouldBe(1);
    }
}
