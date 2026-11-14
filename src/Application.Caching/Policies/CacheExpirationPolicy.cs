// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Calculates finite publication-relative freshness once, independently of cleanup and foreground activity sampling.</summary>
/// <example><code>var lifetime = CacheExpirationPolicy.Create(options, descriptor, false, timeProvider.GetUtcNow());</code></example>
public static class CacheExpirationPolicy
{
    /// <summary>Moves already selected freshness durations to the provider's authoritative publication preparation time without sampling jitter again.</summary>
    /// <param name="metadata">The validated staged metadata carrying its initial expiry and hard-cap durations.</param>
    /// <param name="publication">The UTC instant sampled while the provider holds publication authority.</param>
    /// <returns>A metadata snapshot whose complete selected lifetime starts at the new publication instant.</returns>
    /// <exception cref="ArgumentNullException">The staged metadata is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The selected lifetime cannot fit within the supported timestamp range.</exception>
    /// <example><code>metadata = CacheExpirationPolicy.Rebase(metadata, timeProvider.GetUtcNow());</code></example>
    public static CacheEntryMetadata Rebase(CacheEntryMetadata metadata, DateTimeOffset publication)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var initial = metadata.ExpiresAt - metadata.PublishedAt;
        var absolute = metadata.AbsoluteExpiresAt - metadata.PublishedAt;
        publication = publication.ToUniversalTime();
        return metadata with
        {
            PublishedAt = publication,
            ExpiresAt = publication.Add(initial),
            AbsoluteExpiresAt = publication.Add(absolute),
            LastAccessAt = metadata.HasForegroundAccess && metadata.LastAccessAt < publication ? metadata.LastAccessAt : publication
        };
    }

    /// <summary>Validates bounded overrides and samples downward jitter once. Negative markers never renew sliding idle time.</summary>
    public static Result<CacheEntryLifetime> Create(CacheCollectionOptions options, CacheEntryDescriptor descriptor,
        bool isNotFound, DateTimeOffset publication, Func<double> jitterSample = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        descriptor ??= new();
        var policy = options.Expiration;
        var maximum = policy.SlidingEnabled ? policy.AbsoluteCap : policy.AbsoluteTtl;
        var lifetime = descriptor.AbsoluteTtl ?? maximum;
        var idle = policy.SlidingEnabled ? descriptor.SlidingIdleTimeout ?? policy.SlidingIdleTimeout : (TimeSpan?)null;
        if (lifetime <= TimeSpan.Zero || lifetime > maximum ||
            (descriptor.SlidingIdleTimeout.HasValue && (!policy.SlidingEnabled || descriptor.SlidingIdleTimeout <= TimeSpan.Zero ||
                descriptor.SlidingIdleTimeout > policy.SlidingIdleTimeout)))
        {
            return Result<CacheEntryLifetime>.Failure(new CacheError(CacheErrorCode.Contract, "Entry freshness overrides exceed the collection policy."));
        }

        if (isNotFound)
        {
            lifetime = lifetime < options.NegativeCaching.Ttl ? lifetime : options.NegativeCaching.Ttl;
            idle = null;
        }

        if (!double.IsFinite(policy.JitterRatio) || policy.JitterRatio is < 0 or >= 1)
        {
            return Result<CacheEntryLifetime>.Failure(new CacheError(CacheErrorCode.Validation, "The lifetime jitter ratio is invalid."));
        }

        if (policy.JitterRatio > 0)
        {
            var sample = (jitterSample ?? Random.Shared.NextDouble)();
            if (!double.IsFinite(sample) || sample is < 0 or >= 1)
            {
                return Result<CacheEntryLifetime>.Failure(new CacheError(CacheErrorCode.Contract, "The jitter sampler returned an invalid value."));
            }

            lifetime = TimeSpan.FromTicks(Math.Max(1, lifetime.Ticks - (long)(lifetime.Ticks * policy.JitterRatio * sample)));
        }

        try
        {
            var cap = publication.Add(lifetime);
            var expires = idle.HasValue && idle.Value < lifetime ? publication.Add(idle.Value) : cap;
            return Result<CacheEntryLifetime>.Success(new(publication.ToUniversalTime(), expires.ToUniversalTime(), cap.ToUniversalTime(), idle));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<CacheEntryLifetime>.Failure(new CacheError(CacheErrorCode.Contract, "The finite lifetime exceeds the supported UTC range."));
        }
    }
}

/// <summary>Contains one publication's fixed freshness boundaries.</summary>
/// <param name="PublishedAt">Publication UTC instant.</param>
/// <param name="ExpiresAt">Initial logical expiry.</param>
/// <param name="AbsoluteExpiresAt">Persisted one-time jittered hard cap.</param>
/// <param name="SlidingIdleTimeout">Authoritative renewal interval, absent for fixed TTL and markers.</param>
public readonly record struct CacheEntryLifetime(DateTimeOffset PublishedAt, DateTimeOffset ExpiresAt,
    DateTimeOffset AbsoluteExpiresAt, TimeSpan? SlidingIdleTimeout);