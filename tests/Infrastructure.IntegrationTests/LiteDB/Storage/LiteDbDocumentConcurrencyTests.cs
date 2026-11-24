// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Storage;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Shared.Storage;

/// <summary>Verifies the shared document mutation contract against real native transactions.</summary>
[IntegrationTest("Infrastructure")]
public sealed class LiteDbDocumentConcurrencyTests : DocumentStoreConcurrencyContractTests, IAsyncDisposable
{
    private readonly LiteDbStorageTestHost host = new();

    /// <inheritdoc />
    protected override IDocumentStoreProvider CreateProvider() => this.host.Documents;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => this.host.DisposeAsync();
}
