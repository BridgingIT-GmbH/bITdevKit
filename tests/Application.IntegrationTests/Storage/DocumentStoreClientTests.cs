// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;

[IntegrationTest("Application")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class DocumentStoreClientTests // TODO: implement the tests with the InMemoryProvider together
{
    private readonly TestEnvironmentFixture fixture;
    private readonly IDocumentStoreClient<PersonStub> sut;

    public DocumentStoreClientTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture;
        this.sut = new DocumentStoreClient<PersonStub>(
            new InMemoryDocumentStoreProvider(XunitLoggerFactory.Create(output)));
    }
}