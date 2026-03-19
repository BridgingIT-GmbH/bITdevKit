// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;
using System.IO.Compression;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent builder for configuring <see cref="TemplateOptions"/>.
/// </summary>
public sealed class TemplateOptionsBuilder : IOptionsBuilder<TemplateOptions>
{
    private TemplateOptions target = new();

    /// <summary>
    /// Gets the current target options instance.
    /// </summary>
    public TemplateOptions Target => this.target;

    object IOptionsBuilder.Target => this.Target;

    /// <summary>
    /// Builds the configured template options.
    /// </summary>
    /// <returns>The configured template options.</returns>
    public TemplateOptions Build()
    {
        return this.target;
    }

    /// <summary>
    /// Sets the template format.
    /// </summary>
    /// <param name="format">The format to generate.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder As(Format format)
    {
        this.target = this.target with { Format = format };
        return this;
    }

    /// <summary>
    /// Sets the template format to Excel.
    /// </summary>
    public TemplateOptionsBuilder AsExcel() => this.As(Format.Excel);

    /// <summary>
    /// Sets the template format to CSV.
    /// </summary>
    public TemplateOptionsBuilder AsCsv() => this.As(Format.Csv);

    /// <summary>
    /// Sets the template format to typed CSV.
    /// </summary>
    public TemplateOptionsBuilder AsCsvTyped() => this.As(Format.CsvTyped);

    /// <summary>
    /// Sets the template format to JSON.
    /// </summary>
    public TemplateOptionsBuilder AsJson() => this.As(Format.Json);

    /// <summary>
    /// Sets the template format to XML.
    /// </summary>
    public TemplateOptionsBuilder AsXml() => this.As(Format.Xml);

    /// <summary>
    /// Sets the template format to PDF.
    /// </summary>
    public TemplateOptionsBuilder AsPdf() => this.As(Format.Pdf);

    /// <summary>
    /// Sets the profile name to use.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithProfileName(string profileName)
    {
        this.target = this.target with { ProfileName = profileName };
        return this;
    }

    /// <summary>
    /// Sets whether attribute-based configuration should be used.
    /// </summary>
    /// <param name="value">A value indicating whether attributes should be used.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder UseAttributes(bool value = true)
    {
        this.target = this.target with { UseAttributes = value };
        return this;
    }

    /// <summary>
    /// Sets the culture to use for formatting.
    /// </summary>
    /// <param name="culture">The culture to use.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithCulture(CultureInfo culture)
    {
        this.target = this.target with { Culture = culture ?? CultureInfo.InvariantCulture };
        return this;
    }

    /// <summary>
    /// Sets the sheet or section name.
    /// </summary>
    /// <param name="sheetName">The sheet or section name.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithSheetName(string sheetName)
    {
        this.target = this.target with { SheetName = sheetName };
        return this;
    }

    /// <summary>
    /// Sets the template annotation style.
    /// </summary>
    /// <param name="annotationStyle">The annotation style.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithAnnotationStyle(TemplateAnnotationStyle annotationStyle)
    {
        this.target = this.target with { AnnotationStyle = annotationStyle };
        return this;
    }

    /// <summary>
    /// Sets whether field hints should be included.
    /// </summary>
    /// <param name="value">A value indicating whether field hints should be included.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder IncludeHints(bool value = true)
    {
        this.target = this.target with { IncludeHints = value };
        return this;
    }

    /// <summary>
    /// Sets the number of sample items to include for wrapper-based formats.
    /// </summary>
    /// <param name="count">The number of sample items.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithSampleItemCount(int count)
    {
        this.target = this.target with { SampleItemCount = count < 0 ? 0 : count };
        return this;
    }

    /// <summary>
    /// Sets whether metadata wrappers should be used for structured formats.
    /// </summary>
    /// <param name="value">A value indicating whether metadata wrappers should be used.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder UseMetadataWrapper(bool value = true)
    {
        this.target = this.target with { UseMetadataWrapper = value };
        return this;
    }

    /// <summary>
    /// Sets the payload compression or packaging settings.
    /// </summary>
    /// <param name="compression">The compression settings.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithCompression(PayloadCompressionOptions compression)
    {
        this.target = this.target with { Compression = compression ?? PayloadCompressionOptions.None };
        return this;
    }

    /// <summary>
    /// Enables GZip payload compression for the template.
    /// </summary>
    /// <param name="compressionLevel">The optional compression level.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithGZipCompression(CompressionLevel? compressionLevel = null)
    {
        return this.WithCompression(new PayloadCompressionOptions
        {
            Kind = PayloadCompressionKind.GZip,
            CompressionLevel = compressionLevel
        });
    }

    /// <summary>
    /// Enables ZIP packaging for the template.
    /// </summary>
    /// <param name="entryName">The optional ZIP entry name.</param>
    /// <param name="compressionLevel">The optional compression level.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithZipCompression(string entryName = null, CompressionLevel? compressionLevel = null)
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
    public TemplateOptionsBuilder WithProviderOption(string key, object value)
    {
        var options = this.target.ProviderOptions?.ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, object>();
        options[key] = value;
        this.target = this.target with { ProviderOptions = options };
        return this;
    }

    /// <summary>
    /// Merges provider-specific options into the builder target.
    /// </summary>
    /// <param name="providerOptions">The provider-specific options to merge.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TemplateOptionsBuilder WithProviderOptions(IReadOnlyDictionary<string, object> providerOptions)
    {
        var options = this.target.ProviderOptions?.ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, object>();

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
