// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Text;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Time.Testing;

public partial class InMemoryCacheStoreTests
{
    /// <summary>Opaque continuations do not disclose a redacted key when their ordinary Base64 representation is decoded.</summary>
    [Fact]
    public async Task QueryContinuation_EncryptsPrivateSortKeysAndRejectsTampering()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        Require(await Store(sut, time, "cursor-secret-one", [1]));
        Require(await Store(sut, time, "cursor-secret-two", [2]));
        var query = new CacheMetadataQuery("app", "cards", CacheSelection.Pattern("*"), time.GetUtcNow(), PageSize: 1);
        var first = Require(await sut.QueryAsync(query));
        first.Continuation.ShouldNotBeNull();
        var encoded = first.Continuation.Replace('-', '+').Replace('_', '/');
        var body = Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4));
        Encoding.UTF8.GetString(body).ShouldNotContain("CURSOR-SECRET-ONE");
        Encoding.UTF8.GetString(body).ShouldNotContain("CURSOR-SECRET-TWO");
        body[^1] ^= 1;
        var tampered = Convert.ToBase64String(body).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        (await sut.QueryAsync(query with { Continuation = tampered })).IsFailure.ShouldBeTrue();
        var next = Require(await sut.QueryAsync(query with { Continuation = first.Continuation }));
        next.Entries.Count.ShouldBe(1);
        next.Entries[0].Identity.Key.ShouldNotBe(first.Entries[0].Identity.Key);
    }

    /// <summary>Valid maximum-size control-character keys remain pageable inside the strict compact plaintext bound.</summary>
    [Fact]
    public async Task QueryContinuation_MaximumControlCharacterKeys_PreservePaging()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        var prefix = new string('\u0001', 4090);
        Require(await Store(sut, time, prefix + "firstx", [1]));
        Require(await Store(sut, time, prefix + "second", [2]));
        var query = new CacheMetadataQuery("app", "cards", CacheSelection.Pattern("*"), time.GetUtcNow(), PageSize: 1);
        var first = Require(await sut.QueryAsync(query));
        first.Continuation.ShouldNotBeNull();
        var next = Require(await sut.QueryAsync(query with { Continuation = first.Continuation }));
        next.Entries.Count.ShouldBe(1);
        next.Entries[0].Identity.Key.ShouldNotBe(first.Entries[0].Identity.Key);
    }
}