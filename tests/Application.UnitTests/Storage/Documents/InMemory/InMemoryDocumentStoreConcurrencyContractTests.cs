// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using BridgingIT.DevKit.Shared.Storage;

/// <summary>Runs whole-document concurrency requirements against the in-memory provider.</summary>
[UnitTest("Application")]
public class InMemoryDocumentStoreConcurrencyContractTests : DocumentStoreConcurrencyContractTests
{
    /// <inheritdoc />
    protected override IDocumentStoreProvider CreateProvider() => new InMemoryDocumentStoreProvider();
}
