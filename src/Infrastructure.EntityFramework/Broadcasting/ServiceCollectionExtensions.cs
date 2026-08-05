// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Adds the Entity Framework Broadcasting registry provider.</summary>
/// <example><code>services.AddBroadcasting().WithEntityFrameworkRegistry&lt;AppDbContext&gt;();</code></example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Selects an application-owned Entity Framework registry for the shared runtime.
    /// </summary>
    /// <remarks>
    /// Initial node registration automatically coordinates with the optional
    /// <see cref="IDatabaseReadyService"/> using <typeparamref name="TContext"/>'s type name.
    /// </remarks>
    /// <typeparam name="TContext">The application DbContext implementing Broadcasting persistence.</typeparam>
    /// <example><code>services.AddBroadcasting().WithEntityFrameworkRegistry&lt;AppDbContext&gt;();</code></example>
    public static BroadcastingBuilderContext WithEntityFrameworkRegistry<TContext>(
        this BroadcastingBuilderContext context
    )
        where TContext : DbContext, IBroadcastingContext
    {
        ArgumentNullException.ThrowIfNull(context);

        context.UseRegistryProvider(typeof(EntityFrameworkBroadcastRegistryStore<TContext>));
        context.Options.WaitForDatabaseReady = true;
        context.Options.DatabaseReadyName = typeof(TContext).Name;
        return context;
    }
}
