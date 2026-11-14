// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records explicit hot-entry settings. Strict authoritative validation is mandatory.</summary>
/// <example><code>hot.WithRepresentation(CacheHotEntryRepresentation.Binary).WithMaxEntries(1000);</code></example>
public sealed class CacheHotEntryBuilder : PatchOptionsBuilder<CacheHotEntryOptions, CacheHotEntryBuilder>
{
    /// <summary>Selects binary, approved decoded, or combined representation.</summary>
    /// <param name="value">The representation retained under the existing hot budget.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>hot.WithRepresentation(CacheHotEntryRepresentation.Binary);</code></example>
    public CacheHotEntryBuilder WithRepresentation(CacheHotEntryRepresentation value) => this.With(options => options.Representation, value);

    /// <summary>Selects strict validation; unsupported values fail finalization.</summary>
    /// <param name="value">The authoritative validation mode.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>hot.WithValidation(CacheHotEntryValidation.Strict);</code></example>
    public CacheHotEntryBuilder WithValidation(CacheHotEntryValidation value) => this.With(options => options.Validation, value);

    /// <summary>Sets a finite collection hot-byte ceiling.</summary>
    /// <param name="bytes">The combined retained hot byte ceiling.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>hot.WithMaxResidentBytes(ByteSize.Megabytes(32));</code></example>
    public CacheHotEntryBuilder WithMaxResidentBytes(long bytes) => this.With(options => options.MaxResidentBytes, bytes);

    /// <summary>Sets the finite retained hot entry count.</summary>
    /// <param name="count">The retained entry ceiling.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>hot.WithMaxEntries(1000);</code></example>
    public CacheHotEntryBuilder WithMaxEntries(int count) => this.With(options => options.MaxEntries, count);

    /// <summary>Sets the combined per-entry representation ceiling.</summary>
    /// <param name="bytes">The combined retained entry byte ceiling.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>hot.WithMaxEntryBytes(ByteSize.Kilobytes(256));</code></example>
    public CacheHotEntryBuilder WithMaxEntryBytes(long bytes) => this.With(options => options.MaxEntryBytes, bytes);

    /// <summary>Sets finite hot idle retention.</summary>
    /// <param name="idle">The idle retention interval.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>hot.WithMaxIdleTime(TimeSpan.FromMinutes(2));</code></example>
    public CacheHotEntryBuilder WithMaxIdleTime(TimeSpan idle) => this.With(options => options.MaxIdleTime, idle);

    /// <summary>Builds the explicit hot policy, defaulting this opt-in builder to enabled before applying overrides.</summary>
    /// <returns>The policy snapshot without starting retention or provider access.</returns>
    /// <example><code>var options = hot.WithMaxEntries(1000).Apply();</code></example>
    public CacheHotEntryOptions Apply() => this.Patch.Apply(new CacheHotEntryOptions { Enabled = true }, "Fluent", new Dictionary<string, string>());
}