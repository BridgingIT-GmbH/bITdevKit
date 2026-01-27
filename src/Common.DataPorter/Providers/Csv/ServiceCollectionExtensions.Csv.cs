// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.DataPorter;
using Configuration;
using Extensions;
using Logging;

/// <summary>
/// Extension methods for configuring CSV DataPorter provider.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the CSV provider for DataPorter.
    /// </summary>
    /// <param name="context">The DataPorter builder context.</param>
    /// <param name="configuration">Optional CSV configuration.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The builder context for method chaining.</returns>
    public static DataPorterBuilderContext WithCsv(
        this DataPorterBuilderContext context,
        CsvConfiguration configuration = null,
        string section = "DataPorter:Csv")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<CsvConfiguration>()
            ?? new CsvConfiguration();

        context.Services.AddSingleton<IDataPorterProvider>(sp =>
            new CsvDataPorterProvider(
                configuration,
                sp.GetService<ILoggerFactory>()));

        return context;
    }

    /// <summary>
    /// Adds the CSV provider for DataPorter with configuration action.
    /// </summary>
    /// <param name="context">The DataPorter builder context.</param>
    /// <param name="configure">Action to configure CSV options.</param>
    /// <returns>The builder context for method chaining.</returns>
    public static DataPorterBuilderContext WithCsv(
        this DataPorterBuilderContext context,
        Action<CsvConfiguration> configure)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var configuration = new CsvConfiguration();
        configure(configuration);

        return context.WithCsv(configuration);
    }
}
