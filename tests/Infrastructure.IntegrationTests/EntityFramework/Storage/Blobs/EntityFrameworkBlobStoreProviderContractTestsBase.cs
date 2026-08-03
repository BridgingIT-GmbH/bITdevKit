// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Application.UnitTests.Storage;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public abstract class EntityFrameworkBlobStoreProviderContractTestsBase(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : BlobStoreProviderContractTests
{
    private ServiceProvider serviceProvider;

    protected override string ProviderName => EntityFrameworkBlobStoreProvider<StubDbContext>.ProviderName;

    protected ITestOutputHelper Output { get; } = output;

    protected TestEnvironmentFixture Fixture { get; } = fixture.WithOutput(output);

    protected abstract StubDbContext CreateDbContext(bool forceNew = false);

    protected override IBlobStoreProvider CreateProvider(BlobStoreOptions options = null)
    {
        this.ResetStore();

        this.serviceProvider?.Dispose();
        var services = new ServiceCollection();
        services.AddScoped(_ => this.CreateDbContext(forceNew: true));
        this.serviceProvider = services.BuildServiceProvider(validateScopes: true);

        return new EntityFrameworkBlobStoreProvider<StubDbContext>(
            this.serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            options);
    }

    private void ResetStore()
    {
        using var dbContext = this.CreateDbContext(forceNew: true);
        dbContext.Database.EnsureCreated();

        var chunks = dbContext.StorageBlobChunks.ToList();
        if (chunks.Count != 0)
        {
            dbContext.StorageBlobChunks.RemoveRange(chunks);
        }

        var blobs = dbContext.StorageBlobs.ToList();
        if (blobs.Count != 0)
        {
            dbContext.StorageBlobs.RemoveRange(blobs);
        }

        dbContext.SaveChanges();
    }
}

