// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Extension methods for configuring database transaction options.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures the default database transaction options.
        /// </summary>
        /// <param name="defaultContextName">
        /// The default DbContext name to use when the attribute doesn't specify one.
        /// Can omit the "DbContext" suffix (e.g., "Core" or "CoreDbContext").
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureDatabaseTransactionOptions(string defaultContextName)
        {
            return services.Configure<DatabaseTransactionOptions>(options =>
            {
                options.DefaultContextName = defaultContextName;
            });
        }

        /// <summary>
        /// Configures the default database transaction options using a configuration action.
        /// </summary>
        /// <param name="configureOptions">Action to configure the options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureDatabaseTransactionOptions(Action<DatabaseTransactionOptions> configureOptions)
        {
            return services.Configure(configureOptions);
        }
    }
}
