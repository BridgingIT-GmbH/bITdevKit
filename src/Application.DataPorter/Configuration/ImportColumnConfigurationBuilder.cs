// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

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
