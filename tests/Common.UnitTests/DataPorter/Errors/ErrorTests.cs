// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.DataPorter;

using BridgingIT.DevKit.Common.DataPorter;

[UnitTest("Common")]
public class DataPorterErrorTests
{
    [Fact]
    public void DefaultConstructor_CreatesErrorWithDefaultMessage()
    {
        // Arrange & Act
        var sut = new DataPorterError();

        // Assert
        sut.Message.ShouldBe("A DataPorter error occurred.");
    }

    [Fact]
    public void Constructor_WithMessage_CreatesErrorWithMessage()
    {
        // Arrange
        var message = "Custom error message";

        // Act
        var sut = new DataPorterError(message);

        // Assert
        sut.Message.ShouldBe(message);
    }
}

[UnitTest("Common")]
public class FormatNotSupportedErrorTests
{
    [Fact]
    public void Constructor_WithFormat_CreatesErrorWithFormattedMessage()
    {
        // Arrange
        var format = "XmlSpecial";
        var availableFormats = new[] { "Excel", "Csv", "Json" };

        // Act
        var sut = new FormatNotSupportedError(format, availableFormats);

        // Assert
        sut.Message.ShouldContain(format);
        sut.Message.ShouldContain("Excel");
        sut.Message.ShouldContain("Csv");
        sut.Message.ShouldContain("Json");
    }

    [Fact]
    public void Format_IsStored()
    {
        // Arrange
        var format = "CustomFormat";
        var availableFormats = new[] { "Excel" };

        // Act
        var sut = new FormatNotSupportedError(format, availableFormats);

        // Assert
        sut.Format.ShouldBe(format);
    }

    [Fact]
    public void AvailableFormats_IsStored()
    {
        // Arrange
        var availableFormats = new[] { "Excel", "Csv" };

        // Act
        var sut = new FormatNotSupportedError("Test", availableFormats);

        // Assert
        sut.AvailableFormats.ShouldContain("Excel");
        sut.AvailableFormats.ShouldContain("Csv");
    }

    [Fact]
    public void DefaultConstructor_CreatesErrorWithDefaultMessage()
    {
        // Arrange & Act
        var sut = new FormatNotSupportedError();

        // Assert
        sut.Message.ShouldBe("The requested format is not supported.");
    }
}

[UnitTest("Common")]
public class ExportErrorTests
{
    [Fact]
    public void Constructor_WithMessage_CreatesError()
    {
        // Arrange
        var message = "Export failed";

        // Act
        var sut = new ExportError(message);

        // Assert
        sut.Message.ShouldBe(message);
    }

    [Fact]
    public void Constructor_WithMessageAndException_CreatesError()
    {
        // Arrange
        var message = "Export failed with exception";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var sut = new ExportError(message, innerException);

        // Assert
        sut.Message.ShouldBe(message);
    }
}

[UnitTest("Common")]
public class ImportErrorTests
{
    [Fact]
    public void Constructor_WithMessage_CreatesError()
    {
        // Arrange
        var message = "Import failed";

        // Act
        var sut = new ImportError(message);

        // Assert
        sut.Message.ShouldBe(message);
    }

    [Fact]
    public void Constructor_WithMessageAndException_CreatesError()
    {
        // Arrange
        var message = "Import failed with exception";
        var innerException = new FormatException("Format error");

        // Act
        var sut = new ImportError(message, innerException);

        // Assert
        sut.Message.ShouldBe(message);
    }
}

[UnitTest("Common")]
public class ImportValidationErrorTests
{
    [Fact]
    public void Constructor_WithMessage_CreatesError()
    {
        // Arrange
        var message = "Validation failed";

        // Act
        var sut = new ImportValidationError(message);

        // Assert
        sut.Message.ShouldBe(message);
    }
}
