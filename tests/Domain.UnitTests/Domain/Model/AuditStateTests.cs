// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using BridgingIT.DevKit.Domain.Model;

[UnitTest("Domain")]
public class AuditStateTests
{
    [Fact]
    public void SetCreated_WithValidInput_ShouldSetCreatedProperties()
    {
        // Arrange
        var auditState = new AuditState();
        const string createdBy = "John Doe";
        const string description = "Initial creation";

        // Act
        auditState.SetCreated(createdBy, description);

        // Assert
        auditState.CreatedBy.ShouldBe(createdBy);
        auditState.CreatedDescription.ShouldBe(description);
        auditState.CreatedDate.ShouldNotBe(default(DateTimeOffset));
        auditState.UpdatedDate.ShouldBe(auditState.CreatedDate);
    }

    [Fact]
    public void SetUpdated_WithValidInput_ShouldSetUpdatedProperties()
    {
        // Arrange
        var auditState = new AuditState();
        const string updatedBy = "Jane Smith";
        const string reason = "Data correction";

        // Act
        auditState.SetUpdated(updatedBy, reason);

        // Assert
        auditState.UpdatedBy.ShouldBe(updatedBy);
        auditState.UpdatedDate.ShouldNotBeNull();
        auditState.UpdatedReasons.ShouldContain(reason => reason.Contains(updatedBy) && reason.Contains(reason));
    }

    [Fact]
    public void SetDeactivated_WithValidInput_ShouldSetDeactivatedProperties()
    {
        // Arrange
        var auditState = new AuditState();
        const string deactivatedBy = "Admin User";
        const string reason = "Account suspended";

        // Act
        auditState.SetDeactivated(deactivatedBy, reason);

        // Assert
        auditState.DeactivatedBy.ShouldBe(deactivatedBy);
        auditState.DeactivatedDate.ShouldNotBeNull();
        auditState.Deactivated.Value.ShouldBeTrue();
        auditState.DeactivatedReasons.ShouldContain(reason => reason.Contains(deactivatedBy) && reason.Contains(reason));
        auditState.UpdatedDate.ShouldNotBeNull();
    }

    [Fact]
    public void SetDeleted_WithValidInput_ShouldSetDeletedProperties()
    {
        // Arrange
        var auditState = new AuditState();
        const string deletedBy = "System";
        const string reason = "Data purge";

        // Act
        auditState.SetDeleted(deletedBy, reason);

        // Assert
        auditState.DeletedBy.ShouldBe(deletedBy);
        auditState.DeletedDate.ShouldNotBeNull();
        auditState.Deleted.Value.ShouldBeTrue();
        auditState.DeletedReason.ShouldContain(deletedBy);
        auditState.DeletedReason.ShouldContain(reason);
        auditState.UpdatedDate.ShouldBe(auditState.DeletedDate);
    }

    [Fact]
    public void IsDeactivated_WhenDeactivated_ShouldReturnTrue()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeactivated("Admin", "Test deactivation");

        // Act
        var result = auditState.IsDeactivated();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsDeleted_WhenDeleted_ShouldReturnTrue()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeleted("Admin", "Test deletion");

        // Act
        var result = auditState.IsDeleted();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsUpdated_WhenUpdated_ShouldReturnTrue()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetUpdated("User", "Test update");

        // Act
        var result = auditState.IsUpdated();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void LastActionDate_WhenMultipleActionsPerformed_ShouldReturnLatestDate()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetCreated("Creator", "Initial creation");
        System.Threading.Thread.Sleep(10); // Ensure time difference
        auditState.SetUpdated("Updater", "Update action");
        System.Threading.Thread.Sleep(10); // Ensure time difference
        auditState.SetDeactivated("Deactivator", "Deactivation action");

        // Act
        var lastActionDate = auditState.LastActionDate;

        // Assert
        lastActionDate.ShouldNotBeNull();
        lastActionDate.Value.Ticks.ShouldBe(auditState.DeactivatedDate.Value.Ticks);
    }
}