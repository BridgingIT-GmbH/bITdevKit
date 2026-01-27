// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

using System.Linq.Expressions;

/// <summary>
/// Base class for defining export profiles, similar to AutoMapper Profile.
/// </summary>
/// <typeparam name="TSource">The source type to export.</typeparam>
public abstract class ExportProfileBase<TSource> : IExportProfile<TSource>
    where TSource : class
{
    private readonly List<ColumnConfiguration> columns = [];
    private readonly List<HeaderRowConfiguration> headerRows = [];
    private readonly List<FooterRowConfiguration> footerRows = [];
    private string sheetName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportProfileBase{TSource}"/> class.
    /// </summary>
    protected ExportProfileBase()
    {
        this.Configure();
    }

    /// <inheritdoc/>
    public Type SourceType => typeof(TSource);

    /// <inheritdoc/>
    public IReadOnlyList<ColumnConfiguration> Columns => this.columns;

    /// <inheritdoc/>
    public string SheetName => this.sheetName ?? typeof(TSource).Name;

    /// <inheritdoc/>
    public IReadOnlyList<HeaderRowConfiguration> HeaderRows => this.headerRows;

    /// <inheritdoc/>
    public IReadOnlyList<FooterRowConfiguration> FooterRows => this.footerRows;

    /// <summary>
    /// Override this method to configure export mappings.
    /// </summary>
    protected abstract void Configure();

    /// <summary>
    /// Sets the sheet/section name for this export.
    /// </summary>
    /// <param name="name">The sheet name.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ExportProfileBase<TSource> ToSheet(string name)
    {
        this.sheetName = name;
        return this;
    }

    /// <summary>
    /// Configures a specific column for export.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="propertyExpression">The property expression.</param>
    /// <returns>A column configuration builder.</returns>
    protected ExportColumnConfigurationBuilder<TSource, TProperty> ForColumn<TProperty>(
        Expression<Func<TSource, TProperty>> propertyExpression)
    {
        var memberExpression = propertyExpression.Body as MemberExpression
            ?? throw new ArgumentException("Expression must be a member expression", nameof(propertyExpression));

        var propertyName = memberExpression.Member.Name;
        var propertyInfo = typeof(TSource).GetProperty(propertyName);

        var config = new ColumnConfiguration
        {
            PropertyName = propertyName,
            HeaderName = propertyName,
            PropertyInfo = propertyInfo
        };

        // Compile the expression for efficient value retrieval
        var compiled = propertyExpression.Compile();
        config.ValueGetter = source => compiled((TSource)source);

        this.columns.Add(config);
        return new ExportColumnConfigurationBuilder<TSource, TProperty>(config);
    }

    /// <summary>
    /// Ignores a property during export.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="propertyExpression">The property expression.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ExportProfileBase<TSource> Ignore<TProperty>(
        Expression<Func<TSource, TProperty>> propertyExpression)
    {
        var builder = this.ForColumn(propertyExpression);
        builder.Configuration.Ignore = true;
        return this;
    }

    /// <summary>
    /// Adds a header row to the export.
    /// </summary>
    /// <param name="content">The header row content.</param>
    /// <param name="isBold">Whether the header should be bold.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ExportProfileBase<TSource> AddHeader(string content, bool isBold = true)
    {
        this.headerRows.Add(new HeaderRowConfiguration
        {
            Content = content,
            IsBold = isBold
        });
        return this;
    }

    /// <summary>
    /// Adds a footer row with static content to the export.
    /// </summary>
    /// <param name="content">The footer row content.</param>
    /// <param name="isItalic">Whether the footer should be italic.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ExportProfileBase<TSource> AddFooter(string content, bool isItalic = true)
    {
        this.footerRows.Add(new FooterRowConfiguration
        {
            Content = content,
            IsItalic = isItalic
        });
        return this;
    }

    /// <summary>
    /// Adds a footer row with dynamic content based on the data.
    /// </summary>
    /// <param name="contentFactory">The function to generate footer content.</param>
    /// <param name="isItalic">Whether the footer should be italic.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ExportProfileBase<TSource> AddFooter(
        Func<IEnumerable<TSource>, string> contentFactory,
        bool isItalic = true)
    {
        this.footerRows.Add(new FooterRowConfiguration
        {
            ContentFactory = data => contentFactory(data.Cast<TSource>()),
            IsItalic = isItalic
        });
        return this;
    }
}

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

/// <summary>
/// Builder for configuring conditional styles.
/// </summary>
public sealed class ConditionalStyleBuilder
{
    internal bool IsBold { get; private set; }
    internal bool IsItalic { get; private set; }
    internal string ForegroundColor { get; private set; }
    internal string BackgroundColor { get; private set; }

    /// <summary>
    /// Makes the text bold.
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    public ConditionalStyleBuilder Bold()
    {
        this.IsBold = true;
        return this;
    }

    /// <summary>
    /// Makes the text italic.
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    public ConditionalStyleBuilder Italic()
    {
        this.IsItalic = true;
        return this;
    }

    /// <summary>
    /// Sets the foreground (text) color.
    /// </summary>
    /// <param name="color">The color in hex format (e.g., "#FF0000").</param>
    /// <returns>This builder for method chaining.</returns>
    public ConditionalStyleBuilder WithForegroundColor(string color)
    {
        this.ForegroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets the background color.
    /// </summary>
    /// <param name="color">The color in hex format (e.g., "#FFFF00").</param>
    /// <returns>This builder for method chaining.</returns>
    public ConditionalStyleBuilder WithBackgroundColor(string color)
    {
        this.BackgroundColor = color;
        return this;
    }
}
