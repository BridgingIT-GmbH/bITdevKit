// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Data.Common;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>Verifies publication-relative lifetime after relational staging and native authority waits.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CachePublicationTimeTests</code></example>
public sealed class CachePublicationTimeTests
{
    /// <summary>Preserves once-selected absolute, sliding and negative lifetimes when final staging takes longer than the initial TTL.</summary>
    /// <param name="sliding">Whether the initial idle interval is shorter than the selected absolute cap.</param>
    /// <param name="notFound">Whether the stored entry is a metadata-only negative marker.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PublishAsync_DelayedFinalization_StartsSelectedLifetimeAtPublication(bool sliding, bool notFound)
    {
        // Arrange
        var clock = new Clock();
        var delay = new PublicationDelay(clock);
        var policies = new CacheCollectionOptions
        {
            Expiration = new() { SlidingEnabled = sliding, JitterRatio = 0.5 },
            NegativeCaching = new() { Enabled = notFound }
        };
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: policies, interceptor: delay, timeProvider: clock);
        var sut = fixture.Provider;
        var identity = fixture.Identity("delayed");
        var captured = await sut.CaptureFenceAsync(identity);
        captured.IsSuccess.ShouldBeTrue();
        var started = await sut.BeginWriteAsync(new(identity, captured.Value, default, 4096));
        started.IsSuccess.ShouldBeTrue();
        await using var session = started.Value;
        var bytes = notFound ? Array.Empty<byte>() : new byte[] { 4, 2 };
        await session.Stream.WriteAsync(bytes);
        // These durations represent the result of an earlier, once-only jitter selection.
        var absolute = TimeSpan.FromMilliseconds(7500);
        var initial = sliding ? TimeSpan.FromSeconds(2) : absolute;
        var staged = fixture.Metadata(session, identity.Key, bytes);
        var metadata = staged with
        {
            PublishedAt = clock.Now, LastAccessAt = clock.Now,
            ExpiresAt = clock.Now.Add(initial), AbsoluteExpiresAt = clock.Now.Add(absolute),
            SlidingIdleTimeout = sliding ? initial : null, IsNotFound = notFound,
            PayloadHash = notFound ? null : staged.PayloadHash,
            StoredHash = notFound ? null : staged.StoredHash
        };

        // Act
        delay.Armed = true;
        var published = await session.PublishAsync(metadata);
        var acquired = await sut.LookupAsync(identity, new(OpenPayload: !notFound, ObserveAccess: false));

        // Assert
        delay.Delays.ShouldBe(1);
        clock.Now.ShouldBe(metadata.PublishedAt.AddSeconds(10));
        published.IsSuccess.ShouldBeTrue(string.Join(", ", published.Errors.Select(error => error.Message)));
        published.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        published.Value.Metadata.PublishedAt.ShouldBe(clock.Now);
        published.Value.Metadata.ExpiresAt.ShouldBe(clock.Now.Add(initial));
        published.Value.Metadata.AbsoluteExpiresAt.ShouldBe(clock.Now.Add(absolute));
        acquired.IsSuccess.ShouldBeTrue();
        acquired.Value.State.ShouldBe(CacheReadValidationState.Valid);
        acquired.Value.Metadata.Version.ShouldBe(session.Version);
        acquired.Value.Metadata.PublishedAt.ShouldBe(clock.Now);
        acquired.Value.Metadata.ExpiresAt.ShouldBe(clock.Now.Add(initial));
        acquired.Value.Metadata.AbsoluteExpiresAt.ShouldBe(clock.Now.Add(absolute));
        if (notFound) { acquired.Value.Handle.ShouldBeNull(); }
        else
        {
            await using var handle = acquired.Value.Handle;
            using var copy = new MemoryStream();
            await handle.Stream.CopyToAsync(copy);
            copy.ToArray().ShouldBe(bytes);
        }

        clock.Advance(initial);
        (await sut.LookupAsync(identity, new(ObserveAccess: false))).Value.State.ShouldBe(CacheReadValidationState.Missing);
        (await sut.ReleaseFenceAsync(identity, captured.Value)).IsSuccess.ShouldBeTrue();
    }

    private sealed class PublicationDelay(Clock clock) : DbCommandInterceptor
    {
        /// <summary>Gets or sets whether the next native publication preflight advances the controlled clock.</summary>
        public bool Armed { get; set; }

        /// <summary>Gets the number of native finalization delays injected by this fixture.</summary>
        public int Delays { get; private set; }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (this.Armed)
            {
                this.Armed = false;
                this.Delays++;
                clock.Advance(TimeSpan.FromSeconds(10));
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class Clock : TimeProvider
    {
        /// <summary>Gets the current deterministic UTC instant.</summary>
        public DateTimeOffset Now { get; private set; } = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => this.Now;

        /// <summary>Advances publication and expiry observations without a wall-clock wait.</summary>
        /// <param name="duration">The elapsed duration to add to the controlled clock.</param>
        public void Advance(TimeSpan duration) => this.Now += duration;
    }
}