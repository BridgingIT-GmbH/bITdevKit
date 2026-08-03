// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using Application.UnitTests.Storage;
using Infrastructure.Azure;
using global::Azure.Storage.Blobs;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public sealed class AzureBlobStoreProviderAzuriteContractTests(
    ITestOutputHelper output,
    TestEnvironmentFixture fixture) : BlobStoreProviderContractTests
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);

    protected override string ProviderName => AzureBlobStoreProvider.ProviderName;

    protected override IBlobStoreProvider CreateProvider(BlobStoreOptions options = null)
    {
        this.ResetContractsContainerAsync().GetAwaiter().GetResult();

        return new AzureBlobStoreProvider(
            new BlobServiceClient(this.fixture.AzuriteConnectionString),
            options);
    }

    private async Task ResetContractsContainerAsync()
    {
        var serviceClient = new BlobServiceClient(this.fixture.AzuriteConnectionString);
        var containerClient = serviceClient.GetBlobContainerClient("contracts");

        await containerClient.DeleteIfExistsAsync().ConfigureAwait(false);
    }
}
