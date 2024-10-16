// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

[UnitTest("Common")]
public class PagedResultTests
{
    private readonly IEnumerable<string> messages = ["message1", "message2"];
    private readonly long count = 100;
    private readonly int page = 2;
    private readonly int pageSize = 10;
    private readonly IEnumerable<PersonStub> values = new[] { PersonStub.Create(1), PersonStub.Create(2) }.ToList();

    [Fact]
    public void Success_ShouldSetProperties()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Success(this.values, this.count, this.page, this.pageSize);

        //Act & Assert
        sut.Value.ShouldBe(this.values);
        sut.CurrentPage.ShouldBe(this.page);
        sut.PageSize.ShouldBe(this.pageSize);
        sut.TotalCount.ShouldBe(this.count);
        sut.TotalPages.ShouldBe((int)Math.Ceiling(this.count / (double)this.pageSize));
        sut.HasNextPage.ShouldBeTrue();
        sut.HasPreviousPage.ShouldBeTrue();
        sut.ShouldBeSuccess();
    }

    [Fact]
    public void Success_WithMessage_ShouldSetMessage()
    {
        //Arrange
        const string message = "message1";
        var sut = PagedResult<PersonStub>.Success(this.values, message, this.count, this.page, this.pageSize);

        //Act & Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(message);
    }

    [Fact]
    public void Success_WithMessages_ShouldSetMessages()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Success(this.values, this.messages, this.count, this.page, this.pageSize);

        //Act & Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(this.messages.First());
    }

    [Fact]
    public void Failure_ShouldSetSuccessToFalse()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Failure();

        //Act & Assert
        sut.ShouldBeFailure();
    }

    [Fact]
    public void FailureTError_ShouldAddError()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Failure<NotFoundResultError>();

        //Act & Assert
        sut.ShouldContainError<NotFoundResultError>();
        sut.ShouldBeFailure();
    }

    [Fact]
    public void Failure_ShouldSetMessage()
    {
        //Arrange
        const string message = "message1";

        //Act
        var sut = PagedResult<PersonStub>.Failure(message);

        //Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(message);
    }

    [Fact]
    public void Failure_List_ShouldSetMessages()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Failure(this.messages.ToList());

        //Act & Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(this.messages.First());
    }

    [Fact]
    public void WithMessage_ShouldAddMessage()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Success(this.values);
        const string message = "message1";

        //Act
        sut.WithMessage(message);

        //Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(message);
    }

    [Fact]
    public void WithMessages_ShouldAddMessages()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Success(this.values);

        //Act
        sut.WithMessages(this.messages);

        //Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(this.messages.First());
    }

    [Fact]
    public void WithError_ShouldAddError()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Success(this.values);

        //Act
        sut.WithError(new NotFoundResultError());

        //Assert
        sut.ShouldContainError<NotFoundResultError>();
        sut.ShouldBeFailure();
    }

    [Fact]
    public void WithErrorWithGenericParameter_ShouldAddError()
    {
        //Arrange
        var sut = PagedResult<PersonStub>.Success(this.values);

        //Act
        sut.WithError<NotFoundResultError>();

        //Assert
        sut.ShouldContainError<NotFoundResultError>();
        sut.ShouldBeFailure();
    }
}