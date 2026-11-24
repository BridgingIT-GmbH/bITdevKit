// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Text;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;

/// <summary>Selects independent native Runtime and Operation profiling persistence.</summary>
public static class LiteDbProfilingBuilderExtensions
{
    /// <summary>Registers complete profiling graphs with durable ownership and clear coordination.</summary>
    /// <param name="context">The existing profiling builder.</param>
    /// <param name="configure">Optional native namespace and command bounds.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddProfiling().WithLiteDbProvider();</code></example>
    public static ProfilingBuilderContext WithLiteDbProvider(this ProfilingBuilderContext context, Action<LiteDbFeatureOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = context.Services.RegisterLiteDbFeature("profiling", ["records", "state"], configure);
        var filename = LiteDbDatabaseRegistry.GetOrAdd(context.Services).Get(options.DatabaseName).Connection.Filename;
        var scope = "file:" + HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(filename + "\n" + options.CollectionPrefix));
        return context.WithProvider(sp => new LiteDbProfilingStorageProvider(new(sp.GetRequiredService<IServiceScopeFactory>(), options), scope,
            sp.GetRequiredService<ProfilingOptions>(), sp.GetService<TimeProvider>()));
    }
}
