// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Options for template generation operations.
/// </summary>
public sealed record TemplateOptions
{
    /// <summary>
    /// Gets or sets the format to generate the template in.
    /// </summary>
    public Format Format { get; init; } = Format.Excel;

    /// <summary>
    /// Gets or sets a value indicating whether to use attribute-based configuration.
    /// </summary>
    public bool UseAttributes { get; init; } = true;

    /// <summary>
    /// Gets or sets the culture to use for formatting.
    /// </summary>
    public System.Globalization.CultureInfo Culture { get; init; } = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets the sheet or section name for the template.
    /// </summary>
    public string SheetName { get; init; }

    /// <summary>
    /// Gets or sets the payload compression or packaging settings.
    /// </summary>
    public PayloadCompressionOptions Compression { get; init; } = PayloadCompressionOptions.None;

    /// <summary>
    /// Gets or sets provider-specific options.
    /// </summary>
    public IReadOnlyDictionary<string, object> ProviderOptions { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the annotation style for the generated template.
    /// </summary>
    public TemplateAnnotationStyle AnnotationStyle { get; init; } = TemplateAnnotationStyle.Annotated;

    /// <summary>
    /// Gets or sets a value indicating whether field hints should be included.
    /// </summary>
    public bool IncludeHints { get; init; } = true;

    /// <summary>
    /// Gets or sets the number of sample items to include for wrapper-based formats.
    /// </summary>
    public int SampleItemCount { get; init; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether metadata wrappers should be used for structured formats.
    /// </summary>
    public bool UseMetadataWrapper { get; init; } = true;
}
