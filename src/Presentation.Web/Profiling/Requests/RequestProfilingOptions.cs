// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Configures the HTTP adapter without changing application routing, caching or error responses.</summary>
/// <example><code>profiling.WithRequestProfiling(o => o.StripPathPrefix("/api").Blacklist("/health/**"));</code></example>
public sealed class RequestProfilingOptions
{
    /// <summary>Gets or sets a prefix removed once on a path-segment boundary.</summary>
    /// <example><code>options.StripPathPrefix = "/api";</code></example>
    public string StripPathPrefix { get; set; } = string.Empty;
    /// <summary>Gets or sets anchored exclusion patterns matched against the original incoming path; defaults exclude DevKit, root health and API documentation paths.</summary>
    /// <example><code>options.BlacklistPatterns = ["/_bdk/**", "/health/**"];</code></example>
    public IReadOnlyList<string> BlacklistPatterns { get; set; } = ["/_bdk/**", "/health*", "/swagger/**", "/scalar/**", "/openapi/**"];
    /// <summary>Gets or sets whether ordinary application request-body consumption is observed.</summary>
    /// <example><code>options.ObserveRequestBodyBytes = true;</code></example>
    public bool ObserveRequestBodyBytes { get; set; }
    /// <summary>Gets or sets whether bounded, redacted query arguments are captured.</summary>
    /// <example><code>options.CaptureQueryString = false;</code></example>
    public bool CaptureQueryString { get; set; } = true;
    /// <summary>Gets or sets the query capture limit, from one to 4096 characters.</summary>
    /// <example><code>options.MaximumQueryStringLength = 2048;</code></example>
    public int MaximumQueryStringLength { get; set; } = 4096;
    /// <summary>Gets or sets case-insensitive query parameter names whose values are redacted.</summary>
    /// <example><code>options.RedactedQueryParameters = ["token", "password", "tenantSecret"];</code></example>
    public IReadOnlyList<string> RedactedQueryParameters { get; set; } = ["access_token", "refresh_token", "id_token", "token", "password", "secret", "client_secret", "code", "api_key", "apikey", "authorization"];
    /// <summary>Gets the effective bounded strategy identifier.</summary>
    /// <example><code>var policy = options.StrategyKey;</code></example>
    public string StrategyKey { get; private set; } = "AllRequests";
    /// <summary>Gets the effective bounded policy identifier retained for comparisons.</summary>
    /// <example><code>var configuration = options.ConfigurationKey;</code></example>
    public string ConfigurationKey { get; private set; } = "AllRequests";
    private Func<IServiceProvider, IRequestProfilingSamplingStrategy> factory = _ => new AllRequestsProfilingSamplingStrategy();

    /// <summary>Selects a singleton sampler factory with safe nonempty identifiers.</summary>
    /// <example><code>options.Select("Custom", "v1", sp => sp.GetRequiredService&lt;MySampler&gt;());</code></example>
    public void Select(string strategyKey, string configurationKey, Func<IServiceProvider, IRequestProfilingSamplingStrategy> factory)
    {
        if (!IsIdentifier(strategyKey) || !IsIdentifier(configurationKey))
        {
            throw new ArgumentException("Sampling identifiers must be bounded safe nonempty text.");
        }

        this.StrategyKey = strategyKey;
        this.ConfigurationKey = configurationKey;
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Copies setup settings into a singleton snapshot without sharing mutable pattern collections.</summary>
    /// <example><code>var effective = options.Snapshot();</code></example>
    public RequestProfilingOptions Snapshot() => new()
    {
        StripPathPrefix = this.StripPathPrefix,
        BlacklistPatterns = this.BlacklistPatterns?.ToArray(),
        ObserveRequestBodyBytes = this.ObserveRequestBodyBytes,
        CaptureQueryString = this.CaptureQueryString, MaximumQueryStringLength = this.MaximumQueryStringLength,
        RedactedQueryParameters = this.RedactedQueryParameters?.ToArray(),
        StrategyKey = this.StrategyKey, ConfigurationKey = this.ConfigurationKey, factory = this.factory,
    };

    /// <summary>Creates the one host sampler during singleton construction.</summary>
    /// <example><code>var sampler = options.CreateSampler(services);</code></example>
    public IRequestProfilingSamplingStrategy CreateSampler(IServiceProvider services) => this.factory(services)
        ?? throw new InvalidOperationException("The sampling factory returned null.");

    /// <summary>Validates safe metadata and prepares the bounded exclusion matcher once.</summary>
    /// <example><code>var matcher = options.Validate();</code></example>
    public RequestProfilingPathMatcher Validate()
    {
        if (this.StripPathPrefix is null || this.StripPathPrefix.Length > 256
            || this.StripPathPrefix.Length > 0 && this.StripPathPrefix[0] != '/'
            || this.StripPathPrefix.Any(c => char.IsControl(c) || c is '?' or '#'))
        {
            throw new InvalidOperationException("The request profiling prefix must be a bounded parsed path.");
        }

        if (this.MaximumQueryStringLength is < 1 or > 4096 || this.RedactedQueryParameters is null
            || this.RedactedQueryParameters.Count > 64 || this.RedactedQueryParameters.Any(value => !IsIdentifier(value)))
        {
            throw new InvalidOperationException("The request profiling query capture limit and redacted parameter names must be bounded and valid.");
        }

        return new(this.BlacklistPatterns);
    }

    /// <summary>Validates safe bounded sampler identifiers and decision codes.</summary>
    /// <example><code>if (!RequestProfilingOptions.IsIdentifier(decision.ReasonCode)) SkipCapture();</code></example>
    public static bool IsIdentifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128
        && value.All(c => !char.IsControl(c) && !char.IsSurrogate(c));
}

/// <summary>Composes explicit HTTP adapter settings on the shared profiling builder.</summary>
/// <example><code>requests.Enabled().WithSampling(s => s.Probability(0.1));</code></example>
public sealed class RequestProfilingOptionsBuilder(RequestProfilingOptions target, ProfilingOptions profiling, IServiceCollection services)
{
    /// <summary>Enables or disables automatic HTTP capture, leaving manual operations independent.</summary>
    /// <example><code>requests.Enabled(false);</code></example>
    public RequestProfilingOptionsBuilder Enabled(bool value = true) { profiling.Requests.Enabled = value; return this; }
    /// <summary>Sets the one boundary-matched prefix removed from default keys.</summary>
    /// <example><code>requests.StripPathPrefix("/api");</code></example>
    public RequestProfilingOptionsBuilder StripPathPrefix(string value) { target.StripPathPrefix = value; return this; }
    /// <summary>Replaces the default exclusion patterns; an empty argument list clears all exclusions.</summary>
    /// <example><code>requests.Blacklist("/health/**", "/swagger/**");</code></example>
    public RequestProfilingOptionsBuilder Blacklist(params string[] patterns) { target.BlacklistPatterns = patterns?.ToArray(); return this; }
    /// <summary>Enables observation of ordinary consumed request-body bytes without extra reads.</summary>
    /// <example><code>requests.ObserveRequestBodyBytes();</code></example>
    public RequestProfilingOptionsBuilder ObserveRequestBodyBytes(bool value = true) { target.ObserveRequestBodyBytes = value; return this; }
    /// <summary>Configures bounded query capture and optionally replaces the redacted parameter names.</summary>
    /// <example><code>requests.QueryString(enabled: true, maximumLength: 2048, redactedParameters: ["token", "password"]);</code></example>
    public RequestProfilingOptionsBuilder QueryString(bool enabled = true, int maximumLength = 4096, IReadOnlyList<string> redactedParameters = null)
    {
        target.CaptureQueryString = enabled;
        target.MaximumQueryStringLength = maximumLength;
        if (redactedParameters is not null) { target.RedactedQueryParameters = redactedParameters.ToArray(); }

        return this;
    }
    /// <summary>Selects one entry sampling policy; the last explicit selection wins.</summary>
    /// <example><code>requests.WithSampling(s => s.RateLimit(25, 50));</code></example>
    public RequestProfilingOptionsBuilder WithSampling(Action<RequestProfilingSamplingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new(target, services));
        return this;
    }
}

/// <summary>Selects validated built-in or singleton custom head-sampling strategies.</summary>
/// <example><code>sampling.AllRequests();</code></example>
public sealed class RequestProfilingSamplingBuilder(RequestProfilingOptions target, IServiceCollection services)
{
    /// <summary>Selects every eligible request, subject to independent recording limits.</summary>
    /// <example><code>sampling.AllRequests();</code></example>
    public RequestProfilingSamplingBuilder AllRequests() { target.Select("AllRequests", "AllRequests", _ => new AllRequestsProfilingSamplingStrategy()); return this; }
    /// <summary>Selects each eligible request independently with a finite probability in [0,1].</summary>
    /// <example><code>sampling.Probability(0.1);</code></example>
    public RequestProfilingSamplingBuilder Probability(double probability)
    {
        _ = new ProbabilityRequestProfilingSamplingStrategy(probability);
        target.Select("Probability", "p=" + probability.ToString("R", CultureInfo.InvariantCulture),
            sp => new ProbabilityRequestProfilingSamplingStrategy(probability, sp.GetService<IRequestProfilingRandomSource>()));
        return this;
    }

    /// <summary>Selects from an initially full monotonic process-local token bucket, without waiting.</summary>
    /// <example><code>sampling.RateLimit(25, 50);</code></example>
    public RequestProfilingSamplingBuilder RateLimit(double requestsPerSecond, int burst)
    {
        _ = new RateLimitRequestProfilingSamplingStrategy(requestsPerSecond, burst);
        target.Select("RateLimit", "rate=" + requestsPerSecond.ToString("R", CultureInfo.InvariantCulture) + ";burst=" + burst.ToString(CultureInfo.InvariantCulture),
            sp => new RateLimitRequestProfilingSamplingStrategy(requestsPerSecond, burst, sp.GetService<TimeProvider>()));
        return this;
    }

    /// <summary>Selects a thread-safe singleton custom sampler, optionally using a singleton factory.</summary>
    /// <example><code>sampling.UseStrategy&lt;MySampler&gt;("business", "v1");</code></example>
    public RequestProfilingSamplingBuilder UseStrategy<TStrategy>(string strategyKey = null, string configurationKey = "default",
        Func<IServiceProvider, TStrategy> factory = null) where TStrategy : class, IRequestProfilingSamplingStrategy
    {
        var existing = services.Where(d => d.ServiceType == typeof(TStrategy)).ToArray();
        if (existing.Any(d => d.Lifetime != ServiceLifetime.Singleton))
        {
            throw new InvalidOperationException("Request sampling strategies must be singleton services.");
        }

        if (existing.Length == 0)
        {
            if (factory is null) { services.AddSingleton<TStrategy>(); }
            else { services.AddSingleton(factory); }
        }

        target.Select(strategyKey ?? typeof(TStrategy).Name, configurationKey, sp =>
        {
            if (services.Any(d => d.ServiceType == typeof(TStrategy) && d.Lifetime != ServiceLifetime.Singleton))
            {
                throw new InvalidOperationException("The final custom sampler registration must be singleton.");
            }

            return sp.GetRequiredService<TStrategy>();
        });
        return this;
    }
}

/// <summary>Validates the complete HTTP configuration at host startup without starting capture.</summary>
/// <example>Registered by WithRequestProfiling, after all fluent settings have been composed.</example>
public sealed class RequestProfilingStartupValidator(RequestProfilingRuntime runtime) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) { runtime.Validate(); return Task.CompletedTask; }
    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Owns one prepared request policy and process-local selected-request concurrency.</summary>
/// <example>Resolved once by the HTTP middleware; it performs no provider calls on requests.</example>
public sealed class RequestProfilingRuntime
{
    private readonly ProfilingOptions profiling;
    private readonly OperationProfilingHealthState health;
    private int active;
    /// <summary>Creates the singleton policy from the final composed settings.</summary>
    /// <example>Created by WithRequestProfiling through DI.</example>
    public RequestProfilingRuntime(ProfilingOptions profiling, RequestProfilingOptions options, IServiceProvider services,
        OperationProfilingHealthState health)
    {
        this.profiling = profiling;
        this.health = health;
        this.Options = options.Snapshot();
        this.Matcher = this.Options.Validate();
        this.Sampler = this.Options.CreateSampler(services);
        this.health.SamplingStrategyKey = this.Options.StrategyKey;
        this.health.SamplingConfigurationKey = this.Options.ConfigurationKey;
    }

    /// <summary>Gets the immutable-in-use configured request settings.</summary>
    /// <example><code>var observe = runtime.Options.ObserveRequestBodyBytes;</code></example>
    public RequestProfilingOptions Options { get; }
    /// <summary>Gets the prepared bounded original-path exclusion matcher.</summary>
    /// <example><code>var excluded = runtime.Matcher.IsMatch(path);</code></example>
    public RequestProfilingPathMatcher Matcher { get; }
    /// <summary>Gets the effective one singleton strategy.</summary>
    /// <example><code>var decision = runtime.Sampler.Decide(context);</code></example>
    public IRequestProfilingSamplingStrategy Sampler { get; }
    /// <summary>Gets the final effective automatic HTTP capture state.</summary>
    /// <example><code>if (runtime.Enabled) EvaluatePolicy();</code></example>
    public bool Enabled => this.profiling.Enabled && this.profiling.Requests.Enabled;
    /// <summary>Gets the common operation key length bound.</summary>
    /// <example><code>var bound = runtime.MaximumKeyLength;</code></example>
    public int MaximumKeyLength => this.profiling.Operations.MaxKeyLength;
    /// <summary>Validates the explicit Operations dependency after complete fluent setup.</summary>
    /// <example><code>runtime.Validate();</code></example>
    public void Validate()
    {
        this.profiling.Validate();
        if (this.Enabled && (this.Options.StrategyKey.Length > this.MaximumKeyLength || this.Options.ConfigurationKey.Length > this.MaximumKeyLength))
        {
            throw new InvalidOperationException("Sampling identifiers exceed the configured operation key bound.");
        }
    }
    /// <summary>Evaluates a policy once, counting faults as skips without propagating sampler exceptions.</summary>
    /// <example><code>var decision = runtime.Decide(entry);</code></example>
    public RequestProfilingSamplingDecision Decide(RequestProfilingSamplingContext context)
    {
        Interlocked.Increment(ref this.health.EligibleRequests);
        try
        {
            var decision = this.Sampler.Decide(context);
            if (decision is null || !RequestProfilingOptions.IsIdentifier(decision.ReasonCode)
                || decision.InclusionProbability is { } p && (!double.IsFinite(p) || p < 0 || p > 1))
            {
                throw new InvalidOperationException("Invalid sampling decision.");
            }

            if (decision.Capture) { Interlocked.Increment(ref this.health.SelectedRequests); }

            return decision;
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.health.SamplingErrors);
            return new(false, "SamplingError");
        }
    }

    /// <summary>Counts an HTTP observation fault without changing application response or execution.</summary>
    /// <example><code>runtime.RecordObservationFailure();</code></example>
    public void RecordObservationFailure() => Interlocked.Increment(ref this.health.ObservationFaults);

    /// <summary>Counts an intentional blacklist exclusion without storing the excluded path.</summary>
    /// <example><code>runtime.Exclude();</code></example>
    public void Exclude() => Interlocked.Increment(ref this.health.BlacklistedRequests);

    /// <summary>Increments distinct selected HTTP executions before recording admission.</summary>
    /// <example><code>var active = runtime.EnterSelected();</code></example>
    public int EnterSelected() => Interlocked.Increment(ref this.active);
    /// <summary>Releases one selected HTTP execution once its request boundary ends.</summary>
    /// <example><code>runtime.LeaveSelected();</code></example>
    public void LeaveSelected() => Interlocked.Decrement(ref this.active);
}
