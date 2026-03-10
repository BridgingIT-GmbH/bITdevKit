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
/// Extension methods for configuring PDF DataPorter provider.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the PDF provider for DataPorter (export only).
    /// </summary>
    /// <param name="context">The DataPorter builder context.</param>
    /// <param name="configuration">Optional PDF configuration.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The builder context for method chaining.</returns>
    public static DataPorterBuilderContext WithPdf(
        this DataPorterBuilderContext context,
        PdfConfiguration configuration = null,
        string section = "DataPorter:Pdf")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<PdfConfiguration>()
            ?? new PdfConfiguration();

        context.Services.AddSingleton<IDataPorterProvider>(sp =>
            new PdfDataPorterProvider(
                configuration,
                sp.GetService<ILoggerFactory>()));

        return context;
    }

    /// <summary>
    /// Adds the PDF provider for DataPorter with configuration action (export only).
    /// </summary>
    /// <param name="context">The DataPorter builder context.</param>
    /// <param name="configure">Action to configure PDF options.</param>
    /// <returns>The builder context for method chaining.</returns>
    public static DataPorterBuilderContext WithPdf(
        this DataPorterBuilderContext context,
        Action<PdfConfiguration> configure)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var configuration = new PdfConfiguration();
        configure(configuration);

        return context.WithPdf(configuration);
    }
}
