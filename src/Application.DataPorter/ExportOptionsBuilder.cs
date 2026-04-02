// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;
using System.IO.Compression;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent builder for configuring <see cref="ExportOptions"/>.
/// </summary>
public sealed class ExportOptionsBuilder : IOptionsBuilder<ExportOptions>
{
    private ExportOptions target = new();

    /// <summary>
    /// Gets the current target options instance.
    /// </summary>
    public ExportOptions Target => this.target;

    object IOptionsBuilder.Target => this.Target;

    /// <summary>
    /// Builds the configured export options.
    /// </summary>
    /// <returns>The configured export options.</returns>
    public ExportOptions Build()
    {
        return this.target;
    }

    /// <summary>
    /// Sets the export format.
    /// </summary>
    /// <param name="format">The format to export to.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder As(Format format)
    {
        this.target = this.target with { Format = format };
        return this;
    }

    /// <summary>
    /// Sets the export format to Excel.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder AsExcel()
    {
        return this.As(Format.Excel);
    }

    /// <summary>
    /// Sets the export format to CSV.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder AsCsv()
    {
        return this.As(Format.Csv);
    }

    /// <summary>
    /// Sets the export format to typed CSV.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder AsCsvTyped()
    {
        return this.As(Format.CsvTyped);
    }

    /// <summary>
    /// Sets the export format to JSON.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder AsJson()
    {
        return this.As(Format.Json);
    }

    /// <summary>
    /// Sets the export format to XML.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder AsXml()
    {
        return this.As(Format.Xml);
    }

    /// <summary>
    /// Sets the export format to PDF.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder AsPdf()
    {
        return this.As(Format.Pdf);
    }

    /// <summary>
    /// Sets whether attribute-based configuration should be used.
    /// </summary>
    /// <param name="value">A value indicating whether attributes should be used.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder UseAttributes(bool value = true)
    {
        this.target = this.target with { UseAttributes = value };
        return this;
    }

    /// <summary>
    /// Sets the culture to use for formatting during export.
    /// </summary>
    /// <param name="culture">The culture to use.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder WithCulture(CultureInfo culture)
    {
        this.target = this.target with { Culture = culture ?? CultureInfo.InvariantCulture };
        return this;
    }

    /// <summary>
    /// Sets the sheet or section name for the export.
    /// </summary>
    /// <param name="sheetName">The sheet or section name.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder WithSheetName(string sheetName)
    {
        this.target = this.target with { SheetName = sheetName };
        return this;
    }

    /// <summary>
    /// Sets the file name to use when exporting to <see cref="FileContent"/>.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder WithFileName(string fileName)
    {
        this.target = this.target with { FileName = fileName };
        return this;
    }

    /// <summary>
    /// Sets whether headers should be included in the export.
    /// </summary>
    /// <param name="value">A value indicating whether headers should be included.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder IncludeHeaders(bool value = true)
    {
        this.target = this.target with { IncludeHeaders = value };
        return this;
    }

    /// <summary>
    /// Sets the export progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder WithProgress(IProgress<ExportProgressReport> progress)
    {
        this.target = this.target with { Progress = progress };
        return this;
    }

    /// <summary>
    /// Sets the export progress callback.
    /// </summary>
    /// <param name="progress">The progress callback.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder WithProgress(Action<ExportProgressReport> progress)
    {
        return this.WithProgress(progress is null ? null : new Progress<ExportProgressReport>(progress));
    }

    /// <summary>
    /// Sets the payload compression or packaging settings.
    /// </summary>
    /// <param name="compression">The compression settings.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder WithCompression(PayloadCompressionOptions compression)
    {
        this.target = this.target with { Compression = compression ?? PayloadCompressionOptions.None };
        return this;
    }

    /// <summary>
    /// Enables GZip payload compression for the export.
    /// </summary>
    /// <param name="compressionLevel">The optional compression level.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder WithGZipCompression(CompressionLevel? compressionLevel = null)
    {
        return this.WithCompression(new PayloadCompressionOptions
        {
            Kind = PayloadCompressionKind.GZip,
            CompressionLevel = compressionLevel
        });
    }

    /// <summary>
    /// Enables ZIP packaging for the export.
    /// </summary>
    /// <param name="entryName">The optional ZIP entry name.</param>
    /// <param name="compressionLevel">The optional compression level.</param>
    /// <returns>The builder instance for chaining.</returns>
    public ExportOptionsBuilder WithZipCompression(string entryName = null, CompressionLevel? compressionLevel = null)
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
    public ExportOptionsBuilder WithProviderOption(string key, object value)
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
    public ExportOptionsBuilder WithProviderOptions(IReadOnlyDictionary<string, object> providerOptions)
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
