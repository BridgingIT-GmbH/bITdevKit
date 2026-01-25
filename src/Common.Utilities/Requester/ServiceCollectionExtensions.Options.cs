// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;

/// <summary>
/// Extension methods for configuring handler behavior options.
/// </summary>
public static partial class RequesterOptionsServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures the default retry options.
        /// </summary>
        /// <param name="defaultCount">The default number of retry attempts.</param>
        /// <param name="defaultDelay">The default delay between retries in milliseconds.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureRetryOptions(int defaultCount, int defaultDelay)
        {
            return services.Configure<RetryOptions>(options =>
            {
                options.DefaultCount = defaultCount;
                options.DefaultDelay = defaultDelay;
            });
        }

        /// <summary>
        /// Configures the default retry options using a configuration action.
        /// </summary>
        /// <param name="configureOptions">Action to configure the options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureRetryOptions(Action<RetryOptions> configureOptions)
        {
            return services.Configure(configureOptions);
        }

        /// <summary>
        /// Configures the default timeout options.
        /// </summary>
        /// <param name="defaultDuration">The default timeout duration in milliseconds.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureTimeoutOptions(int defaultDuration)
        {
            return services.Configure<TimeoutOptions>(options =>
            {
                options.DefaultDuration = defaultDuration;
            });
        }

        /// <summary>
        /// Configures the default timeout options using a configuration action.
        /// </summary>
        /// <param name="configureOptions">Action to configure the options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureTimeoutOptions(Action<TimeoutOptions> configureOptions)
        {
            return services.Configure(configureOptions);
        }

        /// <summary>
        /// Configures the default circuit breaker options.
        /// </summary>
        /// <param name="defaultAttempts">The default number of attempts before the circuit opens.</param>
        /// <param name="defaultBreakDurationSeconds">The default break duration in seconds.</param>
        /// <param name="defaultBackoffMilliseconds">The default backoff time in milliseconds.</param>
        /// <param name="defaultBackoffExponential">The default value for exponential backoff.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureCircuitBreakerOptions(
            int defaultAttempts,
            int defaultBreakDurationSeconds,
            int defaultBackoffMilliseconds,
            bool defaultBackoffExponential = false)
        {
            return services.Configure<CircuitBreakerOptions>(options =>
            {
                options.DefaultAttempts = defaultAttempts;
                options.DefaultBreakDurationSeconds = defaultBreakDurationSeconds;
                options.DefaultBackoffMilliseconds = defaultBackoffMilliseconds;
                options.DefaultBackoffExponential = defaultBackoffExponential;
            });
        }

        /// <summary>
        /// Configures the default circuit breaker options using a configuration action.
        /// </summary>
        /// <param name="configureOptions">Action to configure the options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureCircuitBreakerOptions(Action<CircuitBreakerOptions> configureOptions)
        {
            return services.Configure(configureOptions);
        }

        /// <summary>
        /// Configures the default chaos injection options.
        /// </summary>
        /// <param name="defaultInjectionRate">The default injection rate (0.0 to 1.0).</param>
        /// <param name="defaultEnabled">The default enabled state.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureChaosOptions(double defaultInjectionRate, bool defaultEnabled = true)
        {
            return services.Configure<ChaosOptions>(options =>
            {
                options.DefaultInjectionRate = defaultInjectionRate;
                options.DefaultEnabled = defaultEnabled;
            });
        }

        /// <summary>
        /// Configures the default chaos injection options using a configuration action.
        /// </summary>
        /// <param name="configureOptions">Action to configure the options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection ConfigureChaosOptions(Action<ChaosOptions> configureOptions)
        {
            return services.Configure(configureOptions);
        }
    }
}