// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using Application.Storage;

[IntegrationTest("Application")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class DocumentStoreClientTests(ITestOutputHelper output, TestEnvironmentFixture fixture) // TODO: implement the tests with the InMemoryProvider together
{
    private readonly TestEnvironmentFixture fixture = fixture;
    private readonly IDocumentStoreClient<PersonStub> sut = new DocumentStoreClient<PersonStub>(new InMemoryDocumentStoreProvider(XunitLoggerFactory.Create(output)));
}