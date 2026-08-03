// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using Application.UnitTests.Storage;
using Infrastructure.Azure;

[IntegrationTest("Infrastructure")]
public sealed class AzureBlobStoreProviderContractTests : BlobStoreProviderContractTests
{
    private RecordingAzureBlobStoreBackend backend;

    protected override string ProviderName => AzureBlobStoreProvider.ProviderName;

    protected override IBlobStoreProvider CreateProvider(BlobStoreOptions options = null)
    {
        this.backend = new RecordingAzureBlobStoreBackend(options);
        return this.backend;
    }

    protected override void ResetContentReadProbe() => this.backend.OpenReadCalls = 0;

    protected override void AssertContentWasNotReadForMetadataOperations() =>
        this.backend.OpenReadCalls.ShouldBe(0);
}
