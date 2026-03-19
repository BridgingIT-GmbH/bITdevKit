// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Builder for configuring export column settings.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TProperty">The property type.</typeparam>
public sealed class ExportColumnConfigurationBuilder<TSource, TProperty>
    where TSource : class
{
    internal ColumnConfiguration Configuration { get; }

    internal ExportColumnConfigurationBuilder(ColumnConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    /// <summary>
    /// Sets the column header name.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> HasName(string name)
    {
        this.Configuration.HeaderName = name;
        return this;
    }

    /// <summary>
    /// Sets the column order (0-based index).
    /// </summary>
    /// <param name="order">The column order.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> HasOrder(int order)
    {
        this.Configuration.Order = order;
        return this;
    }

    /// <summary>
    /// Sets the column width (for Excel/PDF).
    /// </summary>
    /// <param name="width">The column width.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> HasWidth(double width)
    {
        this.Configuration.Width = width;
        return this;
    }

    /// <summary>
    /// Applies a format string to the value.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> HasFormat(string format)
    {
        this.Configuration.Format = format;
        return this;
    }

    /// <summary>
    /// Sets a null/empty value placeholder.
    /// </summary>
    /// <param name="nullValue">The value to display when null.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> NullAs(string nullValue)
    {
        this.Configuration.NullValue = nullValue;
        return this;
    }

    /// <summary>
    /// Transforms the value before export.
    /// </summary>
    /// <param name="transformer">The transformation function.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> Transform(
        Func<TProperty, object> transformer)
    {
        var originalGetter = this.Configuration.ValueGetter;
        this.Configuration.ValueGetter = source =>
        {
            var value = originalGetter(source);
            return transformer((TProperty)value);
        };
        return this;
    }

    /// <summary>
    /// Sets alignment for the column.
    /// </summary>
    /// <param name="horizontal">The horizontal alignment.</param>
    /// <param name="vertical">The vertical alignment.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> Align(
        HorizontalAlignment horizontal,
        VerticalAlignment vertical = VerticalAlignment.Middle)
    {
        this.Configuration.HorizontalAlignment = horizontal;
        this.Configuration.VerticalAlignment = vertical;
        return this;
    }

    /// <summary>
    /// Applies conditional styling based on value.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="configure">The style configuration action.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> StyleWhen(
        Func<TProperty, bool> condition,
        Action<ConditionalStyleBuilder> configure)
    {
        var styleBuilder = new ConditionalStyleBuilder();
        configure(styleBuilder);

        this.Configuration.ConditionalStyles.Add(new ConditionalStyle
        {
            Condition = value => condition((TProperty)value),
            IsBold = styleBuilder.IsBold,
            IsItalic = styleBuilder.IsItalic,
            ForegroundColor = styleBuilder.ForegroundColor,
            BackgroundColor = styleBuilder.BackgroundColor
        });

        return this;
    }

    /// <summary>
    /// Sets a custom value converter.
    /// </summary>
    /// <param name="converter">The value converter.</param>
    /// <returns>This builder for method chaining.</returns>
    public ExportColumnConfigurationBuilder<TSource, TProperty> UseConverter(IValueConverter converter)
    {
        this.Configuration.Converter = converter;
        return this;
    }
}
