// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Application.DataPorter;

#region Test Entities

/// <summary>
/// Simple test entity without any DataPorter attributes.
/// </summary>
public class SimpleEntity
{
    public int Id { get; set; }

    public string Name { get; set; }
}

/// <summary>
/// Entity with sheet attribute.
/// </summary>
[DataPorterSheet("TestSheet")]
public class EntityWithSheetAttribute
{
    public int Id { get; set; }
}

/// <summary>
/// Entity with sheet and index attribute.
/// </summary>
[DataPorterSheet("ImportSheet", Index = 2)]
public class EntityWithSheetIndexAttribute
{
    public int Id { get; set; }
}

/// <summary>
/// Entity with column attributes.
/// </summary>
public class EntityWithColumnAttributes
{
    [DataPorterColumn("Display Name", Order = 1, Format = "0.00", Width = 100)]
    public string Name { get; set; }
}

/// <summary>
/// Entity with ignore attribute.
/// </summary>
public class EntityWithIgnoreAttribute
{
    public int Id { get; set; }

    [DataPorterIgnore]
    public string IgnoredProperty { get; set; }
}

/// <summary>
/// Entity with export only ignore.
/// </summary>
public class EntityWithIgnoreExportOnly
{
    public int Id { get; set; }

    [DataPorterIgnore(ExportOnly = true)]
    public string ExportIgnored { get; set; }
}

/// <summary>
/// Entity with export disabled on a property.
/// </summary>
public class EntityWithExportDisabled
{
    public int Id { get; set; }

    [DataPorterColumn(Export = false)]
    public string NoExport { get; set; }
}

/// <summary>
/// Entity with ordered columns.
/// </summary>
public class EntityWithOrderedColumns
{
    [DataPorterColumn(Order = 1)]
    public string First { get; set; }

    [DataPorterColumn(Order = 2)]
    public string Second { get; set; }

    [DataPorterColumn(Order = 0)]
    public string Third { get; set; }
}

/// <summary>
/// Entity with required column.
/// </summary>
public class EntityWithRequiredColumn
{
    public int Id { get; set; }

    [DataPorterColumn(Required = true)]
    public string RequiredField { get; set; }
}

/// <summary>
/// Entity with validation attributes.
/// </summary>
public class EntityWithValidation
{
    public int Id { get; set; }

    [DataPorterValidation(ValidationType.Email)]
    public string Email { get; set; }

    [DataPorterValidation(ValidationType.MinLength, Parameter = 5)]
    public string MinLengthField { get; set; }
}

/// <summary>
/// Entity with import disabled on a property.
/// </summary>
public class EntityWithImportDisabled
{
    public int Id { get; set; }

    [DataPorterColumn(Import = false)]
    public string NoImport { get; set; }
}

/// <summary>
/// Entity with read-only property.
/// </summary>
public class EntityWithReadOnlyProperty
{
    public int Id { get; set; }

    public string ReadOnlyProperty => "ReadOnly";
}

/// <summary>
/// Entity with converter attribute.
/// </summary>
public class EntityWithConverter
{
    public int Id { get; set; }

    [DataPorterConverter(typeof(BooleanYesNoConverter))]
    public bool IsActive { get; set; }
}

/// <summary>
/// Test entity used for export profile tests.
/// </summary>
public class TestExportEntity
{
    public int Id { get; set; }

    public string Name { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// Test entity used for import profile tests.
/// </summary>
public class TestImportEntity
{
    public int Id { get; set; }

    public string Name { get; set; }

    public DateTime Date { get; set; }
}

#endregion

#region Test Profiles

/// <summary>
/// Test export profile.
/// </summary>
public class TestExportProfile : IExportProfile<TestExportEntity>
{
    public Type SourceType => typeof(TestExportEntity);

    public IReadOnlyList<ColumnConfiguration> Columns { get; } =
    [
        new ColumnConfiguration { PropertyName = "Id", HeaderName = "Identifier", Order = 0 },
        new ColumnConfiguration { PropertyName = "Name", HeaderName = "Name", Order = 1 },
        new ColumnConfiguration { PropertyName = "Amount", HeaderName = "Total Amount", Order = 2, Format = "C2" }
    ];

    public string SheetName => "TestExportSheet";

    public IReadOnlyList<HeaderRowConfiguration> HeaderRows { get; } = [];

    public IReadOnlyList<FooterRowConfiguration> FooterRows { get; } = [];
}

/// <summary>
/// Test import profile.
/// </summary>
public class TestImportProfile : IImportProfile<TestImportEntity>
{
    public Type TargetType => typeof(TestImportEntity);

    public IReadOnlyList<ImportColumnConfiguration> Columns { get; } =
    [
        new ImportColumnConfiguration { PropertyName = "Id", SourceName = "Identifier" },
        new ImportColumnConfiguration { PropertyName = "Name", SourceName = "Name" },
        new ImportColumnConfiguration { PropertyName = "Date", SourceName = "Date", Format = "yyyy-MM-dd" }
    ];

    public string SheetName => "TestImportSheet";

    public int SheetIndex => -1;

    public int HeaderRowIndex => 0;

    public int SkipRows => 0;

    public ImportValidationBehavior ValidationBehavior => ImportValidationBehavior.CollectErrors;

    public Func<TestImportEntity> Factory => () => new TestImportEntity();

    Func<object> IImportProfile.Factory => () => new TestImportEntity();
}

#endregion

#region Test Providers

/// <summary>
/// Test export provider for unit testing.
/// </summary>
public class TestExportProvider : IDataExportProvider
{
    private readonly bool throwOnCancel;

    public TestExportProvider(Format format = Format.Excel, bool throwOnCancel = false)
    {
        this.Format = format;
        this.throwOnCancel = throwOnCancel;
    }

    public Format Format { get; }

    public IReadOnlyCollection<string> SupportedExtensions => [".xlsx"];

    public bool SupportsImport => false;

    public bool SupportsExport => true;

    public bool SupportsStreaming => false;

    public Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        if (this.throwOnCancel)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        var dataList = data.ToList();
        var bytes = System.Text.Encoding.UTF8.GetBytes("test export data");
        outputStream.Write(bytes, 0, bytes.Length);

        return Task.FromResult(new ExportResult
        {
            BytesWritten = bytes.Length,
            TotalRows = dataList.Count,
            Duration = TimeSpan.Zero,
            Format = this.Format
        });
    }

    public Task<ExportResult> ExportAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var totalRows = dataSets.Sum(ds => ds.Data.Count());
        var bytes = System.Text.Encoding.UTF8.GetBytes("test multi export data");
        outputStream.Write(bytes, 0, bytes.Length);

        return Task.FromResult(new ExportResult
        {
            BytesWritten = bytes.Length,
            TotalRows = totalRows,
            Duration = TimeSpan.Zero,
            Format = this.Format
        });
    }
}

/// <summary>
/// Test import provider for unit testing.
/// </summary>
public class TestImportProvider : IDataImportProvider
{
    private readonly bool throwOnCancel;

    public TestImportProvider(Format format = Format.Excel, bool throwOnCancel = false)
    {
        this.Format = format;
        this.throwOnCancel = throwOnCancel;
    }

    public Format Format { get; }

    public IReadOnlyCollection<string> SupportedExtensions => [".xlsx"];

    public bool SupportsImport => true;

    public bool SupportsExport => false;

    public bool SupportsStreaming => false;

    public Task<ImportResult<TTarget>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        if (this.throwOnCancel)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        var data = new List<TTarget> { new(), new() };

        return Task.FromResult(new ImportResult<TTarget>
        {
            Data = data,
            TotalRows = 2,
            SuccessfulRows = 2,
            FailedRows = 0,
            Duration = TimeSpan.Zero
        });
    }

    public async IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<ValidationResult> ValidateAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        return Task.FromResult(new ValidationResult
        {
            IsValid = true,
            TotalRows = 2,
            ValidRows = 2,
            InvalidRows = 0
        });
    }
}

/// <summary>
/// Test import-only provider (does not support export).
/// </summary>
public class TestImportOnlyProvider : IDataPorterProvider
{
    public Format Format => Format.Excel;

    public IReadOnlyCollection<string> SupportedExtensions => [".xlsx"];

    public bool SupportsImport => true;

    public bool SupportsExport => false;

    public bool SupportsStreaming => false;
}

/// <summary>
/// Test export-only provider (does not support import).
/// </summary>
public class TestExportOnlyProvider : IDataPorterProvider
{
    public Format Format => Format.Excel;

    public IReadOnlyCollection<string> SupportedExtensions => [".xlsx"];

    public bool SupportsImport => false;

    public bool SupportsExport => true;

    public bool SupportsStreaming => false;
}

/// <summary>
/// Test streaming import provider.
/// </summary>
public class TestStreamingImportProvider : IDataImportProvider
{
    public Format Format => Format.Excel;

    public IReadOnlyCollection<string> SupportedExtensions => [".xlsx"];

    public bool SupportsImport => true;

    public bool SupportsExport => false;

    public bool SupportsStreaming => true;

    public Task<ImportResult<TTarget>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var data = new List<TTarget> { new(), new() };

        return Task.FromResult(new ImportResult<TTarget>
        {
            Data = data,
            TotalRows = 2,
            SuccessfulRows = 2,
            FailedRows = 0,
            Duration = TimeSpan.Zero
        });
    }

    public async IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        yield return Result<TTarget>.Success(new TTarget());
        await Task.Delay(1, cancellationToken);
        yield return Result<TTarget>.Success(new TTarget());
    }

    public Task<ValidationResult> ValidateAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        return Task.FromResult(new ValidationResult
        {
            IsValid = true,
            TotalRows = 2,
            ValidRows = 2,
            InvalidRows = 0
        });
    }
}

#endregion
