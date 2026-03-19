// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;
using System.IO.Compression;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent builder for configuring <see cref="ImportOptions"/>.
/// </summary>
public sealed class ImportOptionsBuilder : IOptionsBuilder<ImportOptions>
{
    private ImportOptions target = new();

    /// <summary>
    /// Gets the current target options instance.
    /// </summary>
    public ImportOptions Target => this.target;

    object IOptionsBuilder.Target => this.Target;

    /// <summary>
    /// Builds the configured import options.
    /// </summary>
    /// <returns>The configured import options.</returns>
    public ImportOptions Build()
    {
        return this.target;
    }

    /// <summary>
    /// Sets the import format.
    /// </summary>
    /// <param name="format">The format to import from.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder As(Format format)
    {
        this.target = this.target with { Format = format };
        return this;
    }

    /// <summary>
    /// Sets the import format to Excel.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder AsExcel()
    {
        return this.As(Format.Excel);
    }

    /// <summary>
    /// Sets the import format to CSV.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder AsCsv()
    {
        return this.As(Format.Csv);
    }

    /// <summary>
    /// Sets the import format to typed CSV.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder AsCsvTyped()
    {
        return this.As(Format.CsvTyped);
    }

    /// <summary>
    /// Sets the import format to JSON.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder AsJson()
    {
        return this.As(Format.Json);
    }

    /// <summary>
    /// Sets the import format to XML.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder AsXml()
    {
        return this.As(Format.Xml);
    }

    /// <summary>
    /// Sets whether attribute-based configuration should be used.
    /// </summary>
    /// <param name="value">A value indicating whether attributes should be used.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder UseAttributes(bool value = true)
    {
        this.target = this.target with { UseAttributes = value };
        return this;
    }

    /// <summary>
    /// Sets the culture to use for parsing during import.
    /// </summary>
    /// <param name="culture">The culture to use.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithCulture(CultureInfo culture)
    {
        this.target = this.target with { Culture = culture ?? CultureInfo.InvariantCulture };
        return this;
    }

    /// <summary>
    /// Sets the sheet or section name to import from.
    /// </summary>
    /// <param name="sheetName">The sheet or section name.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithSheetName(string sheetName)
    {
        this.target = this.target with { SheetName = sheetName };
        return this;
    }

    /// <summary>
    /// Sets the sheet index to import from.
    /// </summary>
    /// <param name="sheetIndex">The zero-based sheet index.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithSheetIndex(int? sheetIndex)
    {
        this.target = this.target with { SheetIndex = sheetIndex };
        return this;
    }

    /// <summary>
    /// Sets the header row index.
    /// </summary>
    /// <param name="headerRowIndex">The zero-based header row index.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithHeaderRowIndex(int headerRowIndex)
    {
        this.target = this.target with { HeaderRowIndex = headerRowIndex };
        return this;
    }

    /// <summary>
    /// Sets the number of rows to skip after the header row.
    /// </summary>
    /// <param name="skipRows">The number of rows to skip.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithSkipRows(int skipRows)
    {
        this.target = this.target with { SkipRows = skipRows };
        return this;
    }

    /// <summary>
    /// Sets the validation behavior for the import.
    /// </summary>
    /// <param name="validationBehavior">The validation behavior.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithValidationBehavior(ImportValidationBehavior validationBehavior)
    {
        this.target = this.target with { ValidationBehavior = validationBehavior };
        return this;
    }

    /// <summary>
    /// Sets the maximum number of errors to collect.
    /// </summary>
    /// <param name="maxErrors">The maximum number of errors.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithMaxErrors(int? maxErrors)
    {
        this.target = this.target with { MaxErrors = maxErrors };
        return this;
    }

    /// <summary>
    /// Sets the import progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithProgress(IProgress<ImportProgressReport> progress)
    {
        this.target = this.target with { Progress = progress };
        return this;
    }

    /// <summary>
    /// Sets the import progress callback.
    /// </summary>
    /// <param name="progress">The progress callback.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithProgress(Action<ImportProgressReport> progress)
    {
        return this.WithProgress(progress is null ? null : new Progress<ImportProgressReport>(progress));
    }

    /// <summary>
    /// Sets the payload compression or packaging settings.
    /// </summary>
    /// <param name="compression">The compression settings.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithCompression(PayloadCompressionOptions compression)
    {
        this.target = this.target with { Compression = compression ?? PayloadCompressionOptions.None };
        return this;
    }

    /// <summary>
    /// Enables GZip payload decompression for the import.
    /// </summary>
    /// <param name="compressionLevel">The optional compression level metadata.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithGZipCompression(CompressionLevel? compressionLevel = null)
    {
        return this.WithCompression(new PayloadCompressionOptions
        {
            Kind = PayloadCompressionKind.GZip,
            CompressionLevel = compressionLevel
        });
    }

    /// <summary>
    /// Enables ZIP payload packaging for the import.
    /// </summary>
    /// <param name="entryName">The optional ZIP entry name.</param>
    /// <param name="compressionLevel">The optional compression level metadata.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithZipCompression(string entryName = null, CompressionLevel? compressionLevel = null)
    {
        return this.WithCompression(new PayloadCompressionOptions
        {
            Kind = PayloadCompressionKind.Zip,
            ZipEntryName = entryName,
            CompressionLevel = compressionLevel
        });
    }

    /// <summary>
    /// Adds or updates a provider-specific option.
    /// </summary>
    /// <param name="key">The option key.</param>
    /// <param name="value">The option value.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithProviderOption(string key, object value)
    {
        var options = this.target.ProviderOptions?.ToDictionary(x => x.Key, x => x.Value) ?? [];
        options[key] = value;
        this.target = this.target with { ProviderOptions = options };
        return this;
    }

    /// <summary>
    /// Merges provider-specific options into the builder target.
    /// </summary>
    /// <param name="providerOptions">The provider-specific options to merge.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ImportOptionsBuilder WithProviderOptions(IReadOnlyDictionary<string, object> providerOptions)
    {
        var options = this.target.ProviderOptions?.ToDictionary(x => x.Key, x => x.Value) ?? [];

        if (providerOptions is not null)
        {
            foreach (var (key, value) in providerOptions)
            {
                options[key] = value;
            }
        }

        this.target = this.target with { ProviderOptions = options };
        return this;
    }
}
