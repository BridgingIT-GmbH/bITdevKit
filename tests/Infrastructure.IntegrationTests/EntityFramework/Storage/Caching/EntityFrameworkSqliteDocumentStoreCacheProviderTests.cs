// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkSqliteDocumentStoreCacheProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : EntityFrameworkDocumentStoreCacheProviderTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    [Fact]
    public override async Task GetAsync_WithInvalidKey_ShouldNotReturnValue()
    {
        await base.GetAsync_WithInvalidKey_ShouldNotReturnValue();
    }

    [Fact]
    public override async Task GetAsync_WithValidKey_ShouldReturnValue()
    {
        await base.GetAsync_WithValidKey_ShouldReturnValue();
    }

    [Fact]
    public override void Get_WithInvalidKey_ShouldNotReturnValue()
    {
        base.Get_WithInvalidKey_ShouldNotReturnValue();
    }

    [Fact]
    public override void Get_WithValidKey_ShouldReturnValue()
    {
        base.Get_WithValidKey_ShouldReturnValue();
    }

    [Fact]
    public override async Task RemoveAsync_WithValidKey_ShouldRemoveValue()
    {
        await base.RemoveAsync_WithValidKey_ShouldRemoveValue();
    }

    [Fact]
    public override async Task RemoveStartsWithAsync_WithValidKey_ShouldRemoveValue()
    {
        await base.RemoveStartsWithAsync_WithValidKey_ShouldRemoveValue();
    }

    [Fact]
    public override void RemoveStartsWith_WithValidKey_ShouldRemoveValue()
    {
        base.RemoveStartsWith_WithValidKey_ShouldRemoveValue();
    }

    [Fact]
    public override void Remove_WithValidKey_ShouldRemoveValue()
    {
        base.Remove_WithValidKey_ShouldRemoveValue();
    }

    [Fact]
    public override async Task SetAsync_WithValidData_ShouldReturnValue()
    {
        await base.SetAsync_WithValidData_ShouldReturnValue();
    }

    [Fact]
    public override void Set_WithValidButExpiredData_ShouldNotReturnValue()
    {
        base.Set_WithValidButExpiredData_ShouldNotReturnValue();
    }

    [Fact]
    public override void Set_WithValidDataNoExpiration_ShouldReturnValue()
    {
        base.Set_WithValidDataNoExpiration_ShouldReturnValue();
    }

    [Fact]
    public override void Set_WithValidData_ShouldReturnValue()
    {
        base.Set_WithValidData_ShouldReturnValue();
    }

    [Fact]
    public override async Task TryGetAsync_WithInvalidKey_ShouldReturnFalse()
    {
        await base.TryGetAsync_WithInvalidKey_ShouldReturnFalse();
    }

    [Fact]
    public override async Task TryGetAsync_WithValidKey_ShouldReturnTrue()
    {
        await base.TryGetAsync_WithValidKey_ShouldReturnTrue();
    }

    [Fact]
    public override async Task TryGetKeysAsync_WithExistingEntries_ShouldReturnKeys()
    {
        await base.TryGetKeysAsync_WithExistingEntries_ShouldReturnKeys();
    }

    [Fact]
    public override void TryGetKeys_WithExistingEntries_ShouldReturnKeys()
    {
        base.TryGetKeys_WithExistingEntries_ShouldReturnKeys();
    }

    [Fact]
    public override void TryGet_WithInvalidKey_ShouldReturnFalse()
    {
        base.TryGet_WithInvalidKey_ShouldReturnFalse();
    }

    [Fact]
    public override void TryGet_WithValidKey_ShouldReturnTrue()
    {
        base.TryGet_WithValidKey_ShouldReturnTrue();
    }

    protected override DocumentStoreCacheProvider GetProvider()
    {
        var client = new LoggingDocumentStoreClientBehavior<CacheDocument>(
            XunitLoggerFactory.Create(this.output),
            new DocumentStoreClient<CacheDocument>(new
                EntityFrameworkDocumentStoreProvider<StubDbContext>(
                    this.fixture.EnsureSqliteDbContext(this.output))));

        var provider = new DocumentStoreCacheProvider(
            XunitLoggerFactory.Create(this.output),
            new DocumentStoreCache(client),
            client);

        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);
        provider.Set(this.key, this.value, slidingExpiration, absoluteExpiration);

        return provider;
    }
}