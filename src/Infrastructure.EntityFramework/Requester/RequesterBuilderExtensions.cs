// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring database transaction options on the RequesterBuilder.
/// </summary>
public static class RequesterBuilderExtensions
{
    extension(RequesterBuilder builder)
    {
        /// <summary>
        /// Configures the default database transaction options.
        /// </summary>
        /// <param name="builder">The requester builder.</param>
        /// <param name="defaultContextName">
        /// The default DbContext name to use when the attribute doesn't specify one.
        /// Can omit the "DbContext" suffix (e.g., "Core" or "CoreDbContext").
        /// </param>
        /// <returns>The <see cref="RequesterBuilder"/> instance for fluent chaining.</returns>
        /// <example>
        /// <code>
        /// services.AddRequester()
        ///     .WithBehavior&lt;DatabaseTransactionPipelineBehavior&lt;,&gt;&gt;()
        ///     .WithDatabaseTransactionOptions("Core");
        /// </code>
        /// </example>
        public RequesterBuilder WithDatabaseTransactionOptions(
            string defaultContextName)
        {
            builder.Services.Configure<DatabaseTransactionOptions>(options =>
            {
                options.DefaultContextName = defaultContextName;
            });

            return builder;
        }

        /// <summary>
        /// Configures the default database transaction options using a configuration action.
        /// </summary>
        /// <param name="builder">The requester builder.</param>
        /// <param name="configureOptions">Action to configure the options.</param>
        /// <returns>The <see cref="RequesterBuilder"/> instance for fluent chaining.</returns>
        /// <example>
        /// <code>
        /// services.AddRequester()
        ///     .WithBehavior&lt;DatabaseTransactionPipelineBehavior&lt;,&gt;&gt;()
        ///     .WithDatabaseTransactionOptions(options => options.DefaultContextName = "Core");
        /// </code>
        /// </example>
        public RequesterBuilder WithDatabaseTransactionOptions(
            Action<DatabaseTransactionOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);
            return builder;
        }
    }
}
