// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

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
