// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents the complete template configuration for a type.
/// </summary>
public sealed class TemplateConfiguration
{
    /// <summary>
    /// Gets or sets the target type this template is for.
    /// </summary>
    public Type TargetType { get; set; }

    /// <summary>
    /// Gets or sets the sheet or section name.
    /// </summary>
    public string SheetName { get; set; }

    /// <summary>
    /// Gets the field configurations.
    /// </summary>
    public List<TemplateFieldConfiguration> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the culture to use for formatting.
    /// </summary>
    public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets the payload compression or packaging settings.
    /// </summary>
    public PayloadCompressionOptions Compression { get; set; } = PayloadCompressionOptions.None;

    /// <summary>
    /// Gets or sets the template annotation style.
    /// </summary>
    public TemplateAnnotationStyle AnnotationStyle { get; set; } = TemplateAnnotationStyle.Annotated;

    /// <summary>
    /// Gets or sets a value indicating whether field hints should be included.
    /// </summary>
    public bool IncludeHints { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of sample items to include for wrapper-based formats.
    /// </summary>
    public int SampleItemCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether metadata wrappers should be used for structured formats.
    /// </summary>
    public bool UseMetadataWrapper { get; set; } = true;

    /// <summary>
    /// Gets or sets provider-specific options.
    /// </summary>
    public IReadOnlyDictionary<string, object> ProviderOptions { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents the configuration for a single template field.
/// </summary>
public sealed class TemplateFieldConfiguration
{
    /// <summary>
    /// Gets or sets the mapped property name.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets or sets the external field or header name.
    /// </summary>
    public string HeaderName { get; set; }

    /// <summary>
    /// Gets or sets the field order.
    /// </summary>
    public int Order { get; set; } = -1;

    /// <summary>
    /// Gets or sets the property type.
    /// </summary>
    public Type PropertyType { get; set; } = typeof(string);

    /// <summary>
    /// Gets or sets a value indicating whether this field is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the required message.
    /// </summary>
    public string RequiredMessage { get; set; }

    /// <summary>
    /// Gets or sets the format hint.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets the validation hints for this field.
    /// </summary>
    public List<string> ValidationHints { get; set; } = [];

    /// <summary>
    /// Gets the display type name.
    /// </summary>
    public string TypeName
    {
        get
        {
            var type = Nullable.GetUnderlyingType(this.PropertyType) ?? this.PropertyType;

            if (type == typeof(string))
            {
                return "string";
            }

            if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            {
                return "integer";
            }

            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            {
                return "number";
            }

            if (type == typeof(bool))
            {
                return "boolean";
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return "date";
            }

            if (type == typeof(Guid))
            {
                return "guid";
            }

            return type.Name;
        }
    }

    /// <summary>
    /// Builds a compact human-readable hint string for the field.
    /// </summary>
    /// <returns>The compact hint string.</returns>
    public string BuildHintText()
    {
        var hints = new List<string> { $"type: {this.TypeName}" };

        if (this.IsRequired)
        {
            hints.Add("required");
        }

        if (!string.IsNullOrWhiteSpace(this.Format))
        {
            hints.Add($"format: {this.Format}");
        }

        foreach (var validationHint in this.ValidationHints.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            hints.Add(validationHint);
        }

        return string.Join(" | ", hints);
    }
}
