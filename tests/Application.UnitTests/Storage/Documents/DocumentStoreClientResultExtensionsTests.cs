// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using BridgingIT.DevKit.Common;

[UnitTest("Application")]
public class DocumentStoreClientResultExtensionsTests
{
    private readonly IDocumentStoreClient<PersonStub> sut = Substitute.For<IDocumentStoreClient<PersonStub>>();

    [Fact]
    public async Task CountResultAsync_WithCount_ReturnsSuccess()
    {
        // Arrange
        this.sut.CountAsync(Arg.Any<CancellationToken>())
            .Returns(7);

        // Act
        var result = await this.sut.CountResultAsync();

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(7);
    }

    [Fact]
    public async Task FindResultAsync_WithDocumentKeyAndFilter_ReturnsSuccess()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "42");
        IEnumerable<PersonStub> expected = [new PersonStub { FirstName = "Ada" }];
        this.sut.FindAsync(documentKey, DocumentKeyFilter.FullMatch, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await this.sut.FindResultAsync(documentKey, DocumentKeyFilter.FullMatch);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExistsResultAsync_WithNonTransientException_ReturnsFailure()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "42");
        this.sut.ExistsAsync(documentKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new ArgumentException("invalid key")));

        // Act
        var result = await this.sut.ExistsResultAsync(documentKey);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ExceptionError>();
    }

    [Fact]
    public async Task UpsertResultAsync_WithSuccessfulUpsert_ReturnsSuccess()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "42");
        var entity = new PersonStub { FirstName = "Ada" };
        this.sut.UpsertAsync(documentKey, entity, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await this.sut.UpsertResultAsync(documentKey, entity);

        // Assert
        result.ShouldBeSuccess();
        await this.sut.Received(1).UpsertAsync(documentKey, entity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertResultAsync_WithTimeoutException_ReturnsConcurrencyFailure()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "42");
        var entity = new PersonStub { FirstName = "Ada" };
        this.sut.UpsertAsync(documentKey, entity, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TimeoutException("lease timed out")));

        // Act
        var result = await this.sut.UpsertResultAsync(documentKey, entity);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ConcurrencyError>();
    }

    [Fact]
    public async Task DeleteResultAsync_WithNonTransientException_ReturnsFailure()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "42");
        this.sut.DeleteAsync(documentKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("delete failed")));

        // Act
        var result = await this.sut.DeleteResultAsync(documentKey);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ExceptionError>();
    }

    [Fact]
    public async Task ListResultAsync_WithKeys_ReturnsSuccess()
    {
        // Arrange
        IEnumerable<DocumentKey> expected = [new DocumentKey("people", "42")];
        this.sut.ListAsync(Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await this.sut.ListResultAsync();

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expected);
    }
}
