// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;
using System.IO.Compression;

[UnitTest("Common")]
public class ExportOptionsTests
{
    [Fact]
    public void ExportOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var sut = new ExportOptions();

        // Assert
        sut.Format.ShouldBe(Format.Excel);
        sut.UseAttributes.ShouldBeTrue();
        sut.IncludeHeaders.ShouldBeTrue();
        sut.Culture.ShouldBe(System.Globalization.CultureInfo.InvariantCulture);
        sut.Progress.ShouldBeNull();
        sut.Compression.ShouldBe(PayloadCompressionOptions.None);
        sut.ProviderOptions.ShouldNotBeNull();
        sut.ProviderOptions.ShouldBeEmpty();
    }

    [Fact]
    public void ExportOptions_WithCustomValues_AreSet()
    {
        // Arrange & Act
        var sut = new ExportOptions
        {
            Format = Format.Csv,
            ProfileName = "TestProfile",
            UseAttributes = false,
            Culture = new System.Globalization.CultureInfo("de-DE"),
            SheetName = "Custom Sheet",
            IncludeHeaders = false,
            Progress = new TestProgress<ExportProgressReport>(),
            Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.GZip, CompressionLevel = CompressionLevel.Fastest },
            ProviderOptions = new Dictionary<string, object> { { "key", "value" } }
        };

        // Assert
        sut.Format.ShouldBe(Format.Csv);
        sut.ProfileName.ShouldBe("TestProfile");
        sut.UseAttributes.ShouldBeFalse();
        sut.Culture.Name.ShouldBe("de-DE");
        sut.SheetName.ShouldBe("Custom Sheet");
        sut.IncludeHeaders.ShouldBeFalse();
        sut.Progress.ShouldNotBeNull();
        sut.Compression.Kind.ShouldBe(PayloadCompressionKind.GZip);
        sut.Compression.CompressionLevel.ShouldBe(CompressionLevel.Fastest);
        sut.ProviderOptions["key"].ShouldBe("value");
    }

    [Fact]
    public void ExportOptionsBuilder_WithFluentConfiguration_BuildsExpectedOptions()
    {
        // Arrange
        var progress = new TestProgress<ExportProgressReport>();

        // Act
        var sut = new ExportOptionsBuilder()
            .AsCsv()
            .WithProfileName("ExportProfile")
            .UseAttributes(false)
            .WithCulture(new System.Globalization.CultureInfo("nl-NL"))
            .WithSheetName("Orders")
            .IncludeHeaders(false)
            .WithProgress(progress)
            .WithZipCompression("orders.csv")
            .WithProviderOption("strict", true)
            .Build();

        // Assert
        sut.Format.ShouldBe(Format.Csv);
        sut.ProfileName.ShouldBe("ExportProfile");
        sut.UseAttributes.ShouldBeFalse();
        sut.Culture.Name.ShouldBe("nl-NL");
        sut.SheetName.ShouldBe("Orders");
        sut.IncludeHeaders.ShouldBeFalse();
        sut.Progress.ShouldBe(progress);
        sut.Compression.Kind.ShouldBe(PayloadCompressionKind.Zip);
        sut.Compression.ZipEntryName.ShouldBe("orders.csv");
        sut.ProviderOptions["strict"].ShouldBe(true);
    }
}

[UnitTest("Common")]
public class ImportOptionsTests
{
    [Fact]
    public void ImportOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var sut = new ImportOptions();

        // Assert
        sut.Format.ShouldBe(Format.Excel);
        sut.UseAttributes.ShouldBeTrue();
        sut.HeaderRowIndex.ShouldBe(0);
        sut.SkipRows.ShouldBe(0);
        sut.ValidationBehavior.ShouldBe(ImportValidationBehavior.CollectErrors);
        sut.Culture.ShouldBe(System.Globalization.CultureInfo.InvariantCulture);
        sut.Progress.ShouldBeNull();
        sut.Compression.ShouldBe(PayloadCompressionOptions.None);
        sut.ProviderOptions.ShouldNotBeNull();
        sut.ProviderOptions.ShouldBeEmpty();
    }

    [Fact]
    public void ImportOptions_WithCustomValues_AreSet()
    {
        // Arrange & Act
        var sut = new ImportOptions
        {
            Format = Format.Json,
            ProfileName = "TestProfile",
            UseAttributes = false,
            Culture = new System.Globalization.CultureInfo("fr-FR"),
            SheetName = "Import Sheet",
            SheetIndex = 2,
            HeaderRowIndex = 1,
            SkipRows = 3,
            ValidationBehavior = ImportValidationBehavior.StopImport,
            MaxErrors = 10,
            Progress = new TestProgress<ImportProgressReport>(),
            Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.Zip, ZipEntryName = "payload.json" },
            ProviderOptions = new Dictionary<string, object> { { "strict", true } }
        };

        // Assert
        sut.Format.ShouldBe(Format.Json);
        sut.ProfileName.ShouldBe("TestProfile");
        sut.UseAttributes.ShouldBeFalse();
        sut.Culture.Name.ShouldBe("fr-FR");
        sut.SheetName.ShouldBe("Import Sheet");
        sut.SheetIndex.ShouldBe(2);
        sut.HeaderRowIndex.ShouldBe(1);
        sut.SkipRows.ShouldBe(3);
        sut.ValidationBehavior.ShouldBe(ImportValidationBehavior.StopImport);
        sut.MaxErrors.ShouldBe(10);
        sut.Progress.ShouldNotBeNull();
        sut.Compression.Kind.ShouldBe(PayloadCompressionKind.Zip);
        sut.Compression.ZipEntryName.ShouldBe("payload.json");
        sut.ProviderOptions["strict"].ShouldBe(true);
    }

    [Fact]
    public void ImportOptionsBuilder_WithFluentConfiguration_BuildsExpectedOptions()
    {
        // Arrange
        var progress = new TestProgress<ImportProgressReport>();

        // Act
        var sut = new ImportOptionsBuilder()
            .AsJson()
            .WithProfileName("ImportProfile")
            .UseAttributes(false)
            .WithCulture(new System.Globalization.CultureInfo("fr-FR"))
            .WithSheetName("Orders")
            .WithSheetIndex(1)
            .WithHeaderRowIndex(2)
            .WithSkipRows(3)
            .WithValidationBehavior(ImportValidationBehavior.StopImport)
            .WithMaxErrors(7)
            .WithProgress(progress)
            .WithGZipCompression(CompressionLevel.Fastest)
            .WithProviderOption("strict", true)
            .Build();

        // Assert
        sut.Format.ShouldBe(Format.Json);
        sut.ProfileName.ShouldBe("ImportProfile");
        sut.UseAttributes.ShouldBeFalse();
        sut.Culture.Name.ShouldBe("fr-FR");
        sut.SheetName.ShouldBe("Orders");
        sut.SheetIndex.ShouldBe(1);
        sut.HeaderRowIndex.ShouldBe(2);
        sut.SkipRows.ShouldBe(3);
        sut.ValidationBehavior.ShouldBe(ImportValidationBehavior.StopImport);
        sut.MaxErrors.ShouldBe(7);
        sut.Progress.ShouldBe(progress);
        sut.Compression.Kind.ShouldBe(PayloadCompressionKind.GZip);
        sut.Compression.CompressionLevel.ShouldBe(CompressionLevel.Fastest);
        sut.ProviderOptions["strict"].ShouldBe(true);
    }
}

[UnitTest("Common")]
public class ExportProgressReportTests
{
    [Fact]
    public void ExportProgressReport_WithRequiredProperties_CreatesInstance()
    {
        var sut = new ExportProgressReport
        {
            Operation = "Export",
            Format = Format.Csv,
            ProcessedRows = 25,
            TotalRows = 100,
            PercentageComplete = 25d,
            BytesWritten = 1024,
            IsCompleted = false
        };

        sut.Operation.ShouldBe("Export");
        sut.Format.ShouldBe(Format.Csv);
        sut.ProcessedRows.ShouldBe(25);
        sut.TotalRows.ShouldBe(100);
        sut.PercentageComplete.ShouldBe(25d);
        sut.BytesWritten.ShouldBe(1024);
        sut.IsCompleted.ShouldBeFalse();
        sut.Messages.ShouldBeEmpty();
    }
}

[UnitTest("Common")]
public class ImportProgressReportTests
{
    [Fact]
    public void ImportProgressReport_WithRequiredProperties_CreatesInstance()
    {
        var sut = new ImportProgressReport
        {
            Operation = "Import",
            Format = Format.Json,
            ProcessedRows = 25,
            TotalRows = 100,
            PercentageComplete = 25d,
            SuccessfulRows = 20,
            FailedRows = 5,
            ErrorCount = 5,
            IsCompleted = false
        };

        sut.Operation.ShouldBe("Import");
        sut.Format.ShouldBe(Format.Json);
        sut.ProcessedRows.ShouldBe(25);
        sut.TotalRows.ShouldBe(100);
        sut.PercentageComplete.ShouldBe(25d);
        sut.SuccessfulRows.ShouldBe(20);
        sut.FailedRows.ShouldBe(5);
        sut.ErrorCount.ShouldBe(5);
        sut.IsCompleted.ShouldBeFalse();
        sut.Messages.ShouldBeEmpty();
    }
}

internal sealed class TestProgress<T> : IProgress<T>
{
    public List<T> Reports { get; } = [];

    public void Report(T value)
    {
        this.Reports.Add(value);
    }
}

[UnitTest("Common")]
public class ExportResultTests
{
    [Fact]
    public void ExportResult_WithRequiredProperties_CreatesInstance()
    {
        // Arrange & Act
        var sut = new ExportResult
        {
            BytesWritten = 1024,
            TotalRows = 100,
            Duration = TimeSpan.FromSeconds(5),
            Format = Format.Excel
        };

        // Assert
        sut.BytesWritten.ShouldBe(1024);
        sut.TotalRows.ShouldBe(100);
        sut.Duration.ShouldBe(TimeSpan.FromSeconds(5));
        sut.Format.ShouldBe(Format.Excel);
        sut.Warnings.ShouldNotBeNull();
        sut.Warnings.ShouldBeEmpty();
        sut.Metadata.ShouldNotBeNull();
        sut.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void ExportResult_WithWarningsAndMetadata_CreatesInstance()
    {
        // Arrange & Act
        var sut = new ExportResult
        {
            BytesWritten = 2048,
            TotalRows = 50,
            Duration = TimeSpan.FromMilliseconds(500),
            Format = Format.Csv,
            Warnings = ["Warning 1", "Warning 2"],
            Metadata = new Dictionary<string, object> { { "source", "test" } }
        };

        // Assert
        sut.Warnings.Count.ShouldBe(2);
        sut.Metadata["source"].ShouldBe("test");
    }
}

[UnitTest("Common")]
public class ImportResultTests
{
    [Fact]
    public void ImportResult_WithRequiredProperties_CreatesInstance()
    {
        // Arrange
        var data = new List<SimpleEntity>
        {
            new() { Id = 1, Name = "Item 1" },
            new() { Id = 2, Name = "Item 2" }
        };

        // Act
        var sut = new ImportResult<SimpleEntity>
        {
            Data = data,
            TotalRows = 2,
            SuccessfulRows = 2,
            FailedRows = 0,
            Duration = TimeSpan.FromSeconds(1)
        };

        // Assert
        sut.Data.Count.ShouldBe(2);
        sut.TotalRows.ShouldBe(2);
        sut.SuccessfulRows.ShouldBe(2);
        sut.FailedRows.ShouldBe(0);
        sut.Duration.ShouldBe(TimeSpan.FromSeconds(1));
        sut.Errors.ShouldBeEmpty();
        sut.Warnings.ShouldBeEmpty();
        sut.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void ImportResult_WithErrors_HasErrorsReturnsTrue()
    {
        // Arrange & Act
        var sut = new ImportResult<SimpleEntity>
        {
            Data = [],
            TotalRows = 2,
            SuccessfulRows = 1,
            FailedRows = 1,
            Duration = TimeSpan.FromSeconds(1),
            Errors =
            [
                new ImportRowError
                {
                    RowNumber = 2,
                    Column = "Name",
                    Message = "Name is required"
                }
            ]
        };

        // Assert
        sut.HasErrors.ShouldBeTrue();
        sut.Errors.Count.ShouldBe(1);
    }
}

[UnitTest("Common")]
public class ImportRowErrorTests
{
    [Fact]
    public void ImportRowError_WithRequiredProperties_CreatesInstance()
    {
        // Arrange & Act
        var sut = new ImportRowError
        {
            RowNumber = 5,
            Column = "Email",
            Message = "Invalid email format"
        };

        // Assert
        sut.RowNumber.ShouldBe(5);
        sut.Column.ShouldBe("Email");
        sut.Message.ShouldBe("Invalid email format");
        sut.RawValue.ShouldBeNull();
        sut.Severity.ShouldBe(ErrorSeverity.Error);
    }

    [Fact]
    public void ImportRowError_WithOptionalProperties_CreatesInstance()
    {
        // Arrange & Act
        var sut = new ImportRowError
        {
            RowNumber = 10,
            Column = "Amount",
            Message = "Value truncated",
            RawValue = "123.456789",
            Severity = ErrorSeverity.Warning
        };

        // Assert
        sut.RawValue.ShouldBe("123.456789");
        sut.Severity.ShouldBe(ErrorSeverity.Warning);
    }
}

[UnitTest("Common")]
public class ValidationResultTests
{
    [Fact]
    public void ValidationResult_WithRequiredProperties_CreatesInstance()
    {
        // Arrange & Act
        var sut = new ValidationResult
        {
            IsValid = true,
            TotalRows = 100,
            ValidRows = 100,
            InvalidRows = 0
        };

        // Assert
        sut.IsValid.ShouldBeTrue();
        sut.TotalRows.ShouldBe(100);
        sut.ValidRows.ShouldBe(100);
        sut.InvalidRows.ShouldBe(0);
        sut.Errors.ShouldBeEmpty();
        sut.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void ValidationResult_Success_CreatesValidResult()
    {
        // Arrange & Act
        var sut = ValidationResult.Success(50);

        // Assert
        sut.IsValid.ShouldBeTrue();
        sut.TotalRows.ShouldBe(50);
        sut.ValidRows.ShouldBe(50);
        sut.InvalidRows.ShouldBe(0);
    }
}
