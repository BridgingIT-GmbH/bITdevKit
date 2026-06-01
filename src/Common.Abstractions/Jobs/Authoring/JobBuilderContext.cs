// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the shared builder context for jobs registration.
/// </summary>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .WithJob&lt;CleanupJob&gt;("cleanup", job =&gt; job
///         .WithDescription("Removes stale records.")
///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
/// </code>
/// </example>
public class JobBuilderContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobBuilderContext"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="registrations">The registration store.</param>
    public JobBuilderContext(
        IServiceCollection services,
        IJobRegistrationStore registrations)
    {
        this.Services = services ?? throw new ArgumentNullException(nameof(services));
        this.Registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the registration store.
    /// </summary>
    public IJobRegistrationStore Registrations { get; }
}
