// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
public class AuditStateTests
{
    private readonly AuditState sut;

    public AuditStateTests()
    {
        this.sut = new AuditState();
    }

    [Fact]
    public void SetCreated_SetsCreatedBy()
    {
        // Arrange
        var createdBy = "John Doe";

        // Act
        this.sut.SetCreated(createdBy);

        // Assert
        this.sut.CreatedBy.ShouldBe(createdBy);
        this.sut.IsDeleted().ShouldBeFalse();
        this.sut.IsUpdated().ShouldBeFalse();
        //this.auditState.CreatedDate.ShouldNotBeNull();
    }

    [Fact]
    public void SetUpdated_SetsUpdatedBy()
    {
        // Arrange
        var updatedBy = "John Doe";

        // Act
        this.sut.SetUpdated(by: updatedBy);

        // Assert
        this.sut.UpdatedBy.ShouldBe(updatedBy);
        this.sut.UpdatedDate.ShouldNotBeNull();
        this.sut.IsUpdated().ShouldBeTrue();
    }

    [Fact]
    public void SetDeactivated_SetsDeactivatedBy()
    {
        // Arrange
        var deactivatedBy = "John Doe";

        // Act
        this.sut.SetDeactivated(by: deactivatedBy);

        // Assert
        this.sut.DeactivatedBy.ShouldBe(deactivatedBy);
        this.sut.DeactivatedDate.ShouldNotBeNull();
        this.sut.IsDeactivated().ShouldBeTrue();
        this.sut.IsUpdated().ShouldBeTrue();
    }

    [Fact]
    public void SetDeleted_SetsDeletedBy()
    {
        // Arrange
        var deletedBy = "John Doe";

        // Act
        this.sut.SetDeleted(by: deletedBy);

        // Assert
        this.sut.DeletedBy.ShouldBe(deletedBy);
        this.sut.IsDeleted().ShouldBeTrue();
    }

    [Fact]
    public void SetDeleted_SetsDeletedReason()
    {
        // Arrange
        var deletedReason = "Invalid data";

        // Act
        this.sut.SetDeleted(reason: deletedReason);

        // Assert
        this.sut.DeletedReason.ShouldContain(deletedReason);
        this.sut.IsDeleted().ShouldBeTrue();
        this.sut.IsUpdated().ShouldBeTrue();
    }

    [Fact]
    public void IsDeleted_WhenDeletedPropertyIsNotSet_ReturnsFalse()
    {
        // Arrange & Act
        var result = this.sut.IsDeleted();

        // Assert
        result.ShouldBeFalse();
    }
}