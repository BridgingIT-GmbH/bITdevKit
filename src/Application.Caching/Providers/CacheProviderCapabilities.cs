// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Declares real store capabilities, including native I/O rather than only asynchronous API signatures.</summary>
/// <param name="Persistent">Whether versions survive a process restart.</param>
/// <param name="SharedStoreCoordination">Whether ownership is authoritative across participating nodes.</param>
/// <param name="ForwardOnlyRead">Whether payloads can be read without materialization.</param>
/// <param name="SeekableRead">Whether a stable raw representation can seek.</param>
/// <param name="NativeAsyncIO">Whether native I/O is asynchronous. SQLite reports false.</param>
/// <param name="LocalHostOnly">Whether all participants must share one host.</param>
/// <example><code>var capabilities = provider.Capabilities;</code></example>
public sealed record CacheProviderCapabilities(
    bool Persistent, bool SharedStoreCoordination, bool ForwardOnlyRead,
    bool SeekableRead, bool NativeAsyncIO, bool LocalHostOnly);

/// <summary>Defines a collection's persisted compatibility identity without runtime CLR type names.</summary>
/// <param name="Namespace">Application namespace.</param>
/// <param name="Collection">Logical collection name.</param>
/// <param name="ContractVersion">Contract version isolated in entry identity.</param>
/// <param name="ContractId">Stable registered contract alias.</param>
/// <param name="Dimensions">Immutable declared dimension schema.</param>
/// <param name="Policies">Effective finite collection policies for provider quota, readers, and reclamation.</param>
/// <example><code>await provider.InitializeAsync([binding], cancellationToken);</code></example>
public sealed record CacheProviderCollection(string Namespace, string Collection, string ContractVersion,
    string ContractId, IReadOnlyList<CacheDimensionSchema> Dimensions, CacheCollectionOptions Policies = null);

/// <summary>Represents an opaque store-issued publication fence, never a caller-invented epoch.</summary>
/// <param name="Value">The provider-owned serialized token.</param>
public readonly record struct CachePublicationFence(string Value);

/// <summary>Represents confirmed shared-store source authority.</summary>
/// <param name="Value">The opaque provider-issued owner token.</param>
public readonly record struct CacheOwnerToken(string Value);