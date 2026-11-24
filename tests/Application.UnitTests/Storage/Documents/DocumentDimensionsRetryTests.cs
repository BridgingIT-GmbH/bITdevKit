// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public sealed class DocumentDimensionsRetryTests
{
    [Fact]
    public async Task DimensionsUpdate_RegistrationBudgetExhausted_IsNotRetried()
    {
        var inner = Substitute.For<IDocumentStoreClient<Invoice>>();
        inner.UpdateDimensionsAsync(Arg.Any<DocumentDimensionsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Result<DocumentInfo>.Failure(new DocumentDimensionRegistrationContentionError()));
        var sut = new RetryDocumentStoreClientBehavior<Invoice>(null, inner, new() { Attempts = 3 });
        var result = await sut.UpdateDimensionsAsync(new() { Key = new("archive", "owner"), Dimensions = DocumentDimensions.Empty });
        result.HasError<DocumentDimensionRegistrationContentionError>().ShouldBeTrue();
        await inner.Received(1).UpdateDimensionsAsync(Arg.Any<DocumentDimensionsUpdate>(), Arg.Any<CancellationToken>());
    }

    public sealed class Invoice { }
}
