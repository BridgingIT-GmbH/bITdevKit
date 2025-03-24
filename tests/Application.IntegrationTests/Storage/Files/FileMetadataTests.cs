// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;

public class FileMetadataTests
{
    [Fact]
    public void GetParentPath_ShouldReturnParentPath_WhenPathHasMultipleSegments()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = "folder/subfolder/file.txt" };

        // Act
        var parentPath = fileMetadata.GetParentPath();

        // Assert
        parentPath.ShouldBe("folder/subfolder");
    }

    [Fact]
    public void GetParentPath_ShouldReturnNull_WhenPathIsEmpty()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = string.Empty };

        // Act
        var parentPath = fileMetadata.GetParentPath();

        // Assert
        parentPath.ShouldBeNull();
    }

    [Fact]
    public void GetParentPath_ShouldReturnNull_WhenPathDoesNotContainSlash()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = "file.txt" };

        // Act
        var parentPath = fileMetadata.GetParentPath();

        // Assert
        parentPath.ShouldBeNull();
    }

    [Fact]
    public void GetParentPath_ShouldReturnRoot_WhenPathHasSingleSlash()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = "folder/file.txt" };

        // Act
        var parentPath = fileMetadata.GetParentPath();

        // Assert
        parentPath.ShouldBe("folder");
    }

    [Fact]
    public void GetFileName_ShouldReturnFileName_WhenPathHasMultipleSegments()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = "folder/subfolder/file.txt" };

        // Act
        var fileName = fileMetadata.GetFileName();

        // Assert
        fileName.ShouldBe("file.txt");
    }

    [Fact]
    public void GetFileName_ShouldReturnPath_WhenPathDoesNotContainSlash()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = "file.txt" };

        // Act
        var fileName = fileMetadata.GetFileName();

        // Assert
        fileName.ShouldBe("file.txt");
    }

    [Fact]
    public void GetFileName_ShouldReturnNull_WhenPathIsEmpty()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = string.Empty };

        // Act
        var fileName = fileMetadata.GetFileName();

        // Assert
        fileName.ShouldBeNull();
    }

    [Fact]
    public void GetFileExtension_ShouldReturnExtension_WhenPathContainsDot()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = "folder/subfolder/file.txt" };

        // Act
        var fileExtension = fileMetadata.GetFileExtension();

        // Assert
        fileExtension.ShouldBe("txt");
    }

    [Fact]
    public void GetFileExtension_ShouldReturnNull_WhenPathDoesNotContainDot()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = "folder/subfolder/file" };

        // Act
        var fileExtension = fileMetadata.GetFileExtension();

        // Assert
        fileExtension.ShouldBeNull();
    }

    [Fact]
    public void GetFileExtension_ShouldReturnNull_WhenPathIsEmpty()
    {
        // Arrange
        var fileMetadata = new FileMetadata { Path = string.Empty };

        // Act
        var fileExtension = fileMetadata.GetFileExtension();

        // Assert
        fileExtension.ShouldBeNull();
    }
}