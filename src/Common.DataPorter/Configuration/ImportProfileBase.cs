// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

using System.Linq.Expressions;

/// <summary>
/// Base class for defining import profiles.
/// </summary>
/// <typeparam name="TTarget">The target type to import into.</typeparam>
public abstract class ImportProfileBase<TTarget> : IImportProfile<TTarget>
    where TTarget : class, new()
{
    private readonly List<ImportColumnConfiguration> columns = [];
    private string sheetName;
    private int sheetIndex = -1;
    private int headerRowIndex = 0;
    private int skipRows = 0;
    private ImportValidationBehavior validationBehavior = ImportValidationBehavior.CollectErrors;
    private Func<TTarget> factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportProfileBase{TTarget}"/> class.
    /// </summary>
    protected ImportProfileBase()
    {
        this.Configure();
    }

    /// <inheritdoc/>
    public Type TargetType => typeof(TTarget);

    /// <inheritdoc/>
    public IReadOnlyList<ImportColumnConfiguration> Columns => this.columns;

    /// <inheritdoc/>
    IReadOnlyList<ImportColumnConfiguration> IImportProfile.Columns => this.columns;

    /// <inheritdoc/>
    public string SheetName => this.sheetName;

    /// <inheritdoc/>
    public int SheetIndex => this.sheetIndex;

    /// <inheritdoc/>
    public int HeaderRowIndex => this.headerRowIndex;

    /// <inheritdoc/>
    public int SkipRows => this.skipRows;

    /// <inheritdoc/>
    public ImportValidationBehavior ValidationBehavior => this.validationBehavior;

    /// <inheritdoc/>
    public Func<TTarget> Factory => this.factory ?? (() => new TTarget());

    /// <inheritdoc/>
    Func<object> IImportProfile.Factory => () => this.Factory();

    /// <summary>
    /// Override this method to configure import mappings.
    /// </summary>
    protected abstract void Configure();

    /// <summary>
    /// Specifies the sheet/section name to import from.
    /// </summary>
    /// <param name="name">The sheet name.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> FromSheet(string name)
    {
        this.sheetName = name;
        return this;
    }

    /// <summary>
    /// Specifies the sheet index to import from (0-based).
    /// </summary>
    /// <param name="index">The sheet index.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> FromSheet(int index)
    {
        this.sheetIndex = index;
        return this;
    }

    /// <summary>
    /// Specifies the row containing headers.
    /// </summary>
    /// <param name="rowIndex">The row index (0-based).</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> HeaderRow(int rowIndex)
    {
        this.headerRowIndex = rowIndex;
        return this;
    }

    /// <summary>
    /// Skips the specified number of data rows after the header.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> SkipDataRows(int count)
    {
        this.skipRows = count;
        return this;
    }

    /// <summary>
    /// Configures a column mapping for a property.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="propertyExpression">The property expression.</param>
    /// <returns>An import column configuration builder.</returns>
    protected ImportColumnConfigurationBuilder<TTarget, TProperty> ForColumn<TProperty>(
        Expression<Func<TTarget, TProperty>> propertyExpression)
    {
        var memberExpression = propertyExpression.Body as MemberExpression
            ?? throw new ArgumentException("Expression must be a member expression", nameof(propertyExpression));

        var propertyName = memberExpression.Member.Name;
        var propertyInfo = typeof(TTarget).GetProperty(propertyName);

        var config = new ImportColumnConfiguration
        {
            PropertyName = propertyName,
            SourceName = propertyName,
            PropertyInfo = propertyInfo
        };

        this.columns.Add(config);
        return new ImportColumnConfigurationBuilder<TTarget, TProperty>(config);
    }

    /// <summary>
    /// Maps a column by header name to a property.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="headerName">The header name in the source.</param>
    /// <param name="propertyExpression">The property expression.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> MapColumn<TProperty>(
        string headerName,
        Expression<Func<TTarget, TProperty>> propertyExpression)
    {
        var builder = this.ForColumn(propertyExpression);
        builder.FromHeader(headerName);
        return this;
    }

    /// <summary>
    /// Maps a column by index to a property.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="columnIndex">The column index (0-based).</param>
    /// <param name="propertyExpression">The property expression.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> MapColumn<TProperty>(
        int columnIndex,
        Expression<Func<TTarget, TProperty>> propertyExpression)
    {
        var builder = this.ForColumn(propertyExpression);
        builder.FromIndex(columnIndex);
        return this;
    }

    /// <summary>
    /// Ignores a property during import.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="propertyExpression">The property expression.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> Ignore<TProperty>(
        Expression<Func<TTarget, TProperty>> propertyExpression)
    {
        var builder = this.ForColumn(propertyExpression);
        builder.Configuration.Ignore = true;
        return this;
    }

    /// <summary>
    /// Sets a custom factory for creating target instances.
    /// </summary>
    /// <param name="factory">The factory function.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> UseFactory(Func<TTarget> factory)
    {
        this.factory = factory;
        return this;
    }

    /// <summary>
    /// Configures behavior on validation failure.
    /// </summary>
    /// <param name="behavior">The validation behavior.</param>
    /// <returns>This profile for method chaining.</returns>
    protected ImportProfileBase<TTarget> OnValidationFailure(ImportValidationBehavior behavior)
    {
        this.validationBehavior = behavior;
        return this;
    }
}

/// <summary>
/// Builder for configuring import column settings.
/// </summary>
/// <typeparam name="TTarget">The target type.</typeparam>
/// <typeparam name="TProperty">The property type.</typeparam>
public sealed class ImportColumnConfigurationBuilder<TTarget, TProperty>
    where TTarget : class, new()
{
    internal ImportColumnConfiguration Configuration { get; }

    internal ImportColumnConfigurationBuilder(ImportColumnConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    /// <summary>
    /// Maps this property from a specific header name.
    /// </summary>
    /// <param name="headerName">The header name in the source.</param>
    /// <returns>This builder for method chaining.</returns>
    public ImportColumnConfigurationBuilder<TTarget, TProperty> FromHeader(string headerName)
    {
        this.Configuration.SourceName = headerName;
        return this;
    }

    /// <summary>
    /// Maps this property from a specific column index.
    /// </summary>
    /// <param name="index">The column index (0-based).</param>
    /// <returns>This builder for method chaining.</returns>
    public ImportColumnConfigurationBuilder<TTarget, TProperty> FromIndex(int index)
    {
        this.Configuration.SourceIndex = index;
        return this;
    }

    /// <summary>
    /// Marks this column as required.
    /// </summary>
    /// <param name="errorMessage">The error message when validation fails.</param>
    /// <returns>This builder for method chaining.</returns>
    public ImportColumnConfigurationBuilder<TTarget, TProperty> IsRequired(string errorMessage = null)
    {
        this.Configuration.IsRequired = true;
        this.Configuration.RequiredMessage = errorMessage;
        return this;
    }

    /// <summary>
    /// Sets a custom parser for the column value.
    /// </summary>
    /// <param name="parser">The parser function.</param>
    /// <returns>This builder for method chaining.</returns>
    public ImportColumnConfigurationBuilder<TTarget, TProperty> ParseWith(
        Func<string, TProperty> parser)
    {
        this.Configuration.Parser = value => parser(value);
        return this;
    }

    /// <summary>
    /// Transforms the parsed value.
    /// </summary>
    /// <param name="transformer">The transformation function.</param>
    /// <returns>This builder for method chaining.</returns>
    public ImportColumnConfigurationBuilder<TTarget, TProperty> Transform(
        Func<TProperty, TProperty> transformer)
    {
        var originalParser = this.Configuration.Parser;
        if (originalParser is not null)
        {
            this.Configuration.Parser = value =>
            {
                var parsed = (TProperty)originalParser(value);
                return transformer(parsed);
            };
        }
        else
        {
            this.Configuration.Parser = value =>
            {
                var converted = this.Configuration.ConvertValue(value);
                return transformer((TProperty)converted);
            };
        }
        return this;
    }

    /// <summary>
    /// Adds custom validation for import.
    /// </summary>
    /// <param name="validator">The validation function.</param>
    /// <param name="errorMessage">The error message when validation fails.</param>
    /// <returns>This builder for method chaining.</returns>
    public ImportColumnConfigurationBuilder<TTarget, TProperty> Validate(
        Func<TProperty, bool> validator,
        string errorMessage)
    {
        this.Configuration.Validators.Add(new ColumnValidator
        {
            Validate = value => validator((TProperty)value),
            ErrorMessage = errorMessage
        });
        return this;
    }

    /// <summary>
    /// Sets a custom value converter.
    /// </summary>
    /// <param name="converter">The value converter.</param>
    /// <returns>This builder for method chaining.</returns>
    public ImportColumnConfigurationBuilder<TTarget, TProperty> UseConverter(IValueConverter converter)
    {
        this.Configuration.Converter = converter;
        return this;
    }

    /// <summary>
    /// Sets the format string for parsing.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <returns>This builder for method chaining.</returns>
    public ImportColumnConfigurationBuilder<TTarget, TProperty> HasFormat(string format)
    {
        this.Configuration.Format = format;
        return this;
    }
}
