// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

/// <summary>Verifies retry eligibility for Document Storage errors.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DocumentDimensionsRetryTests</code></example>
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

    /// <summary>Verifies explicit and legacy retry classifications.</summary>
    /// <example><code>await tests.UpsertAsync_WithClassifiedFailure_RespectsRetryEligibility("permanent", 1);</code></example>
    [Theory]
    [InlineData("permanent", 1)]
    [InlineData("indeterminate", 1)]
    [InlineData("transient", 3)]
    [InlineData("legacy", 3)]
    public async Task UpsertAsync_WithClassifiedFailure_RespectsRetryEligibility(string kind, int attempts)
    {
        IResultError error = kind switch
        {
            "permanent" => new DocumentStoreProviderError("Constraint", null, isTransient: false),
            "indeterminate" => new DocumentStoreIndeterminateOperationError("upsert", "test"),
            "transient" => new DocumentStoreProviderError("Deadlock", null, isTransient: true),
            _ => new DocumentStoreProviderError("Provider error")
        };
        var inner = Substitute.For<IDocumentStoreClient<Invoice>>();
        inner.UpsertAsync(Arg.Any<DocumentKey>(), Arg.Any<Invoice>(), Arg.Any<DocumentWriteOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result<DocumentInfo>.Failure(error));
        var sut = new RetryDocumentStoreClientBehavior<Invoice>(null, inner, new() { Attempts = 3, Backoff = TimeSpan.Zero });

        var result = await sut.UpsertAsync(new("archive", "invoice"), new());

        result.Errors.ShouldBe([error]);
        await inner.Received(attempts).UpsertAsync(Arg.Any<DocumentKey>(), Arg.Any<Invoice>(), Arg.Any<DocumentWriteOptions>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that cancellation stops before invoking the decorated client.</summary>
    /// <example><code>await tests.UpsertAsync_WithCanceledToken_DoesNotInvokeProvider();</code></example>
    [Fact]
    public async Task UpsertAsync_WithCanceledToken_DoesNotInvokeProvider()
    {
        var inner = Substitute.For<IDocumentStoreClient<Invoice>>();
        var sut = new RetryDocumentStoreClientBehavior<Invoice>(null, inner);
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => sut.UpsertAsync(new("archive", "invoice"), new(), cancellationToken: stopped.Token));

        await inner.DidNotReceiveWithAnyArgs().UpsertAsync(default, default);
    }

    public sealed class Invoice { }
}
