// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Constructs collection engines only from finalized validated startup bindings.</summary>
/// <example><code>var handle = factory.Create(binding, contract, provider);</code></example>
public interface ICacheCollectionFactory
{
    /// <summary>Creates one typed handle and prebuilt behavior chain without provider I/O.</summary>
    ICacheCollection<T> Create<T>(CacheCollectionBinding binding, CacheValueContract<T> contract, ICacheStoreProvider provider);

    /// <summary>Creates one declared binary handle and prebuilt behavior chain without provider I/O.</summary>
    ICacheBinaryCollection CreateBinary(CacheCollectionBinding binding, CacheBinaryValueContract contract, ICacheStoreProvider provider);
}