// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Merges configuration from profiles, attributes, and options.
/// Priority: Profile > Attributes > Options > Defaults
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConfigurationMerger"/> class.
/// </remarks>
/// <param name="profileRegistry">The profile registry.</param>
/// <param name="attributeReader">The attribute configuration reader.</param>
public sealed class ConfigurationMerger(
    IProfileRegistry profileRegistry,
    AttributeConfigurationReader attributeReader)
{
    /// <summary>
    /// Builds the export configuration for a type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="options">The export options.</param>
    /// <returns>The merged export configuration.</returns>
    public ExportConfiguration BuildExportConfiguration<TSource>(ExportOptions options = null)
        where TSource : class
    {
        return this.BuildExportConfiguration(typeof(TSource), options);
    }

    /// <summary>
    /// Builds the export configuration for a type.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="options">The export options.</param>
    /// <returns>The merged export configuration.</returns>
    public ExportConfiguration BuildExportConfiguration(Type sourceType, ExportOptions options = null)
    {
        options ??= new ExportOptions();

        // Start with attribute-based configuration
        var config = options.UseAttributes
            ? attributeReader.ReadExportConfiguration(sourceType)
            : new ExportConfiguration { SourceType = sourceType };
        config = this.CloneExportConfiguration(config);

        // Override with profile configuration if available
        var profile = profileRegistry.GetExportProfile(sourceType);
        if (profile is not null)
        {
            this.MergeProfileIntoExportConfig(config, profile);
        }

        // Apply options
        if (!string.IsNullOrEmpty(options.SheetName))
        {
            config.SheetName = options.SheetName;
        }

        config.Culture = options.Culture;
        config.IncludeHeaders = options.IncludeHeaders;
        config.Progress = options.Progress;
        config.Compression = options.Compression ?? PayloadCompressionOptions.None;

        return config;
    }

    /// <summary>
    /// Builds the import configuration for a type.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="options">The import options.</param>
    /// <returns>The merged import configuration.</returns>
    public ImportConfiguration BuildImportConfiguration<TTarget>(ImportOptions options = null)
        where TTarget : class, new()
    {
        return this.BuildImportConfiguration(typeof(TTarget), options);
    }

    /// <summary>
    /// Builds the import configuration for a type.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    /// <param name="options">The import options.</param>
    /// <returns>The merged import configuration.</returns>
    public ImportConfiguration BuildImportConfiguration(Type targetType, ImportOptions options = null)
    {
        options ??= new ImportOptions();

        // Start with attribute-based configuration
        var config = options.UseAttributes
            ? attributeReader.ReadImportConfiguration(targetType)
            : new ImportConfiguration { TargetType = targetType };
        config = this.CloneImportConfiguration(config);

        // Override with profile configuration if available
        var profile = profileRegistry.GetImportProfile(targetType);
        if (profile is not null)
        {
            this.MergeProfileIntoImportConfig(config, profile);
        }

        // Apply options
        if (!string.IsNullOrEmpty(options.SheetName))
        {
            config.SheetName = options.SheetName;
        }

        if (options.SheetIndex.HasValue)
        {
            config.SheetIndex = options.SheetIndex.Value;
        }

        config.HeaderRowIndex = options.HeaderRowIndex;
        config.SkipRows = options.SkipRows;
        config.ValidationBehavior = options.ValidationBehavior;
        config.MaxErrors = options.MaxErrors;
        config.Culture = options.Culture;
        config.Progress = options.Progress;
        config.Compression = options.Compression ?? PayloadCompressionOptions.None;

        return config;
    }

    private void MergeProfileIntoExportConfig(ExportConfiguration config, IExportProfile profile)
    {
        // Override sheet name from profile
        if (!string.IsNullOrEmpty(profile.SheetName))
        {
            config.SheetName = profile.SheetName;
        }

        // Merge column configurations
        foreach (var profileColumn in profile.Columns)
        {
            var existingColumn = config.Columns.FirstOrDefault(
                c => c.PropertyName == profileColumn.PropertyName);

            if (existingColumn is not null)
            {
                // Merge profile settings into existing column
                this.MergeColumnConfiguration(existingColumn, profileColumn);
            }
            else
            {
                // Add new column from profile
                config.Columns.Add(new ColumnConfiguration
                {
                    PropertyName = profileColumn.PropertyName,
                    HeaderName = profileColumn.HeaderName,
                    Order = profileColumn.Order,
                    Format = profileColumn.Format,
                    Width = profileColumn.Width,
                    NullValue = profileColumn.NullValue,
                    Ignore = profileColumn.Ignore,
                    HorizontalAlignment = profileColumn.HorizontalAlignment,
                    VerticalAlignment = profileColumn.VerticalAlignment,
                    Converter = profileColumn.Converter,
                    PropertyInfo = profileColumn.PropertyInfo,
                    ValueGetter = profileColumn.ValueGetter,
                    ConditionalStyles = profileColumn.ConditionalStyles
                });
            }
        }

        // Add header rows from profile
        config.HeaderRows.AddRange(profile.HeaderRows);

        // Add footer rows from profile
        config.FooterRows.AddRange(profile.FooterRows);

        // Re-sort columns by order
        config.Columns = [.. config.Columns
            .Where(c => !c.Ignore)
            .OrderBy(c => c.Order >= 0 ? c.Order : int.MaxValue)];
    }

    private void MergeColumnConfiguration(ColumnConfiguration target, ColumnConfiguration source)
    {
        if (!string.IsNullOrEmpty(source.HeaderName))
        {
            target.HeaderName = source.HeaderName;
        }

        if (source.Order >= 0)
        {
            target.Order = source.Order;
        }

        if (!string.IsNullOrEmpty(source.Format))
        {
            target.Format = source.Format;
        }

        if (source.Width >= 0)
        {
            target.Width = source.Width;
        }

        if (!string.IsNullOrEmpty(source.NullValue))
        {
            target.NullValue = source.NullValue;
        }

        target.Ignore = source.Ignore;
        target.HorizontalAlignment = source.HorizontalAlignment;
        target.VerticalAlignment = source.VerticalAlignment;

        if (source.Converter is not null)
        {
            target.Converter = source.Converter;
        }

        if (source.ValueGetter is not null)
        {
            target.ValueGetter = source.ValueGetter;
        }

        if (source.ConditionalStyles.Count > 0)
        {
            target.ConditionalStyles.AddRange(source.ConditionalStyles);
        }
    }

    private ExportConfiguration CloneExportConfiguration(ExportConfiguration source)
    {
        return new ExportConfiguration
        {
            SourceType = source.SourceType,
            SheetName = source.SheetName,
            Columns = [.. source.Columns.Select(this.CloneColumnConfiguration)],
            HeaderRows = [.. source.HeaderRows.Select(this.CloneHeaderRowConfiguration)],
            FooterRows = [.. source.FooterRows.Select(this.CloneFooterRowConfiguration)],
            Culture = source.Culture,
            IncludeHeaders = source.IncludeHeaders,
            Progress = source.Progress,
            Compression = source.Compression
        };
    }

    private ImportConfiguration CloneImportConfiguration(ImportConfiguration source)
    {
        return new ImportConfiguration
        {
            TargetType = source.TargetType,
            SheetName = source.SheetName,
            SheetIndex = source.SheetIndex,
            Columns = [.. source.Columns.Select(this.CloneImportColumnConfiguration)],
            HeaderRowIndex = source.HeaderRowIndex,
            SkipRows = source.SkipRows,
            ValidationBehavior = source.ValidationBehavior,
            MaxErrors = source.MaxErrors,
            Culture = source.Culture,
            Progress = source.Progress,
            Compression = source.Compression
        };
    }

    private ColumnConfiguration CloneColumnConfiguration(ColumnConfiguration source)
    {
        return new ColumnConfiguration
        {
            PropertyName = source.PropertyName,
            HeaderName = source.HeaderName,
            Order = source.Order,
            Format = source.Format,
            Width = source.Width,
            NullValue = source.NullValue,
            Ignore = source.Ignore,
            HorizontalAlignment = source.HorizontalAlignment,
            VerticalAlignment = source.VerticalAlignment,
            Converter = source.Converter,
            PropertyInfo = source.PropertyInfo,
            ValueGetter = source.ValueGetter,
            ConditionalStyles = [.. source.ConditionalStyles.Select(this.CloneConditionalStyle)]
        };
    }

    private ImportColumnConfiguration CloneImportColumnConfiguration(ImportColumnConfiguration source)
    {
        return new ImportColumnConfiguration
        {
            PropertyName = source.PropertyName,
            SourceName = source.SourceName,
            SourceIndex = source.SourceIndex,
            Order = source.Order,
            Format = source.Format,
            Width = source.Width,
            NullValue = source.NullValue,
            Ignore = source.Ignore,
            IsRequired = source.IsRequired,
            RequiredMessage = source.RequiredMessage,
            HorizontalAlignment = source.HorizontalAlignment,
            VerticalAlignment = source.VerticalAlignment,
            Converter = source.Converter,
            PropertyInfo = source.PropertyInfo,
            ValueSetter = source.ValueSetter,
            Validators = [.. source.Validators.Select(this.CloneColumnValidator)],
            Parser = source.Parser
        };
    }

    private HeaderRowConfiguration CloneHeaderRowConfiguration(HeaderRowConfiguration source)
    {
        return new HeaderRowConfiguration
        {
            Content = source.Content,
            IsBold = source.IsBold,
            FontSize = source.FontSize,
            HorizontalAlignment = source.HorizontalAlignment
        };
    }

    private FooterRowConfiguration CloneFooterRowConfiguration(FooterRowConfiguration source)
    {
        return new FooterRowConfiguration
        {
            Content = source.Content,
            ContentFactory = source.ContentFactory,
            IsBold = source.IsBold,
            IsItalic = source.IsItalic,
            FontSize = source.FontSize,
            HorizontalAlignment = source.HorizontalAlignment
        };
    }

    private ConditionalStyle CloneConditionalStyle(ConditionalStyle source)
    {
        return new ConditionalStyle
        {
            Condition = source.Condition,
            IsBold = source.IsBold,
            IsItalic = source.IsItalic,
            ForegroundColor = source.ForegroundColor,
            BackgroundColor = source.BackgroundColor
        };
    }

    private ColumnValidator CloneColumnValidator(ColumnValidator source)
    {
        return new ColumnValidator
        {
            Validate = source.Validate,
            ErrorMessage = source.ErrorMessage
        };
    }

    private void MergeProfileIntoImportConfig(ImportConfiguration config, IImportProfile profile)
    {
        // Override sheet settings from profile
        if (!string.IsNullOrEmpty(profile.SheetName))
        {
            config.SheetName = profile.SheetName;
        }

        if (profile.SheetIndex >= 0)
        {
            config.SheetIndex = profile.SheetIndex;
        }

        config.HeaderRowIndex = profile.HeaderRowIndex;
        config.SkipRows = profile.SkipRows;
        config.ValidationBehavior = profile.ValidationBehavior;

        if (profile.Factory is not null)
        {
            config.Factory = profile.Factory;
        }

        // Merge column configurations
        foreach (var profileColumn in profile.Columns)
        {
            var existingColumn = config.Columns.FirstOrDefault(
                c => c.PropertyName == profileColumn.PropertyName);

            if (existingColumn is not null)
            {
                // Merge profile settings into existing column
                this.MergeImportColumnConfiguration(existingColumn, profileColumn);
            }
            else
            {
                // Add new column from profile
                config.Columns.Add(new ImportColumnConfiguration
                {
                    PropertyName = profileColumn.PropertyName,
                    SourceName = profileColumn.SourceName,
                    SourceIndex = profileColumn.SourceIndex,
                    Order = profileColumn.Order,
                    Format = profileColumn.Format,
                    IsRequired = profileColumn.IsRequired,
                    RequiredMessage = profileColumn.RequiredMessage,
                    Ignore = profileColumn.Ignore,
                    Converter = profileColumn.Converter,
                    PropertyInfo = profileColumn.PropertyInfo,
                    ValueSetter = profileColumn.ValueSetter,
                    Parser = profileColumn.Parser,
                    Validators = [.. profileColumn.Validators]
                });
            }
        }
    }

    private void MergeImportColumnConfiguration(ImportColumnConfiguration target, ImportColumnConfiguration source)
    {
        if (!string.IsNullOrEmpty(source.SourceName))
        {
            target.SourceName = source.SourceName;
        }

        if (source.SourceIndex >= 0)
        {
            target.SourceIndex = source.SourceIndex;
        }

        if (source.Order >= 0)
        {
            target.Order = source.Order;
        }

        if (!string.IsNullOrEmpty(source.Format))
        {
            target.Format = source.Format;
        }

        target.IsRequired = source.IsRequired || target.IsRequired;

        if (!string.IsNullOrEmpty(source.RequiredMessage))
        {
            target.RequiredMessage = source.RequiredMessage;
        }

        target.Ignore = source.Ignore;

        if (source.Converter is not null)
        {
            target.Converter = source.Converter;
        }

        if (source.ValueSetter is not null)
        {
            target.ValueSetter = source.ValueSetter;
        }

        if (source.Parser is not null)
        {
            target.Parser = source.Parser;
        }

        if (source.Validators.Count > 0)
        {
            target.Validators.AddRange(source.Validators);
        }
    }
}
