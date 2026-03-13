// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.DataPorter;
using Configuration;
using Extensions;
using Logging;

/// <summary>
/// Extension methods for configuring typed-row CSV DataPorter provider.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the typed-row CSV provider for DataPorter.
    /// </summary>
    /// <param name="context">The DataPorter builder context.</param>
    /// <param name="configuration">Optional typed-row CSV configuration.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The builder context for method chaining.</returns>
    public static DataPorterBuilderContext WithCsvTyped(
        this DataPorterBuilderContext context,
        CsvTypedConfiguration configuration = null,
        string section = "DataPorter:CsvTyped")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<CsvTypedConfiguration>()
            ?? new CsvTypedConfiguration();

        context.Services.AddSingleton<IDataPorterProvider>(sp =>
            new CsvTypedDataPorterProvider(
                configuration,
                sp.GetService<ILoggerFactory>()));

        return context;
    }

    /// <summary>
    /// Adds the typed-row CSV provider for DataPorter with configuration action.
    /// </summary>
    /// <param name="context">The DataPorter builder context.</param>
    /// <param name="configure">Action to configure typed-row CSV options.</param>
    /// <returns>The builder context for method chaining.</returns>
    public static DataPorterBuilderContext WithCsvTyped(
        this DataPorterBuilderContext context,
        Action<CsvTypedConfiguration> configure)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var configuration = new CsvTypedConfiguration();
        configure(configuration);

        return context.WithCsvTyped(configuration);
    }
}
