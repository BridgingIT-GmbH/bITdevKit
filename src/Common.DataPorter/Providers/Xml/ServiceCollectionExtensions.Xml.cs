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
/// Extension methods for configuring XML DataPorter provider.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the XML provider for DataPorter.
    /// </summary>
    /// <param name="context">The DataPorter builder context.</param>
    /// <param name="configuration">Optional XML configuration.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The builder context for method chaining.</returns>
    public static DataPorterBuilderContext WithXml(
        this DataPorterBuilderContext context,
        XmlConfiguration configuration = null,
        string section = "DataPorter:Xml")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<XmlConfiguration>()
            ?? new XmlConfiguration();

        context.Services.AddSingleton<IDataPorterProvider>(sp =>
            new XmlDataPorterProvider(
                configuration,
                sp.GetService<ILoggerFactory>()));

        return context;
    }

    /// <summary>
    /// Adds the XML provider for DataPorter with configuration action.
    /// </summary>
    /// <param name="context">The DataPorter builder context.</param>
    /// <param name="configure">Action to configure XML options.</param>
    /// <returns>The builder context for method chaining.</returns>
    public static DataPorterBuilderContext WithXml(
        this DataPorterBuilderContext context,
        Action<XmlConfiguration> configure)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var configuration = new XmlConfiguration();
        configure(configuration);

        return context.WithXml(configuration);
    }
}
