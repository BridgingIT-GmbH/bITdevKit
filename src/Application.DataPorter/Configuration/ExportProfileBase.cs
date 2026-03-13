// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

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
