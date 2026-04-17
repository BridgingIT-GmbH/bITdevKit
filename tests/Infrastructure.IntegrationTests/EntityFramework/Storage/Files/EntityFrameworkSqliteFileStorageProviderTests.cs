// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class EntityFrameworkSqliteFileStorageProviderTests(ITestOutputHelper output) : EntityFrameworkFileStorageProviderTestsBase, IDisposable
{
    private readonly EntityFrameworkSqliteFileStorageTestSupport support = new(output);

    protected override EntityFrameworkFileStorageOptions DefaultOptions => this.support.DefaultOptions;

    protected override ServiceProvider ServiceProvider => this.support.ServiceProvider;

    protected override IFileStorageProvider CreateInMemoryProvider(string locationName)
        => this.support.CreateInMemoryProvider(locationName);

    protected override EntityFrameworkFileStorageProvider<StubDbContext> CreateProvider(
        string locationName,
        EntityFrameworkFileStorageOptions options = null)
        => this.support.CreateProvider(locationName, options);

    public void Dispose()
    {
        this.support.Dispose();
    }
}
