// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using DevKit.Domain.Model;

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
        auditState.CreatedDate.ShouldNotBe(default);
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
    public void SetActivated_WithValidInput_ShouldSetActivatedProperties()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeactivated("Admin", "Test deactivation");
        const string activatedBy = "Manager";
        const string reason = "Account restored";

        // Act
        auditState.SetActivated(activatedBy, reason);

        // Assert
        auditState.Deactivated.Value.ShouldBeFalse();
        auditState.DeactivatedDate.ShouldBeNull();
        auditState.UpdatedDate.ShouldNotBeNull();
        auditState.UpdatedBy.ShouldBe(activatedBy);
        auditState.UpdatedReasons.ShouldContain(reason => reason.Contains(activatedBy) && reason.Contains("Activated") && reason.Contains(reason));
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
    public void SetUndeleted_WithValidInput_ShouldSetUndeletedProperties()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeleted("System", "Data purge");
        const string undeletedBy = "Support";
        const string reason = "Deletion reversal";

        // Act
        auditState.SetUndeleted(undeletedBy, reason);

        // Assert
        auditState.Deleted.Value.ShouldBeFalse();
        auditState.DeletedDate.ShouldBeNull();
        auditState.DeletedReason.ShouldBeNull();
        auditState.UpdatedDate.ShouldNotBeNull();
        auditState.UpdatedBy.ShouldBe(undeletedBy);
        auditState.UpdatedReasons.ShouldContain(reason => reason.Contains(undeletedBy) && reason.Contains("Undeleted") && reason.Contains(reason));
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
    public void IsActive_WhenNewlyCreated_ShouldReturnTrue()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetCreated("Creator", "Initial creation");

        // Act
        var result = auditState.IsActive();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsActive_WhenDeactivated_ShouldReturnFalse()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeactivated("Admin", "Test deactivation");

        // Act
        var result = auditState.IsActive();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_WhenDeleted_ShouldReturnFalse()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeleted("Admin", "Test deletion");

        // Act
        var result = auditState.IsActive();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_AfterActivation_ShouldReturnTrue()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeactivated("Admin", "Test deactivation");
        auditState.SetActivated("Manager", "Test activation");

        // Act
        var result = auditState.IsActive();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsActive_AfterUndeletion_ShouldReturnTrue()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeleted("Admin", "Test deletion");
        auditState.SetUndeleted("Support", "Test undeletion");

        // Act
        var result = auditState.IsActive();

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
    public void IsDeleted_AfterUndeletion_ShouldReturnFalse()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetDeleted("Admin", "Test deletion");
        auditState.SetUndeleted("Support", "Test undeletion");

        // Act
        var result = auditState.IsDeleted();

        // Assert
        result.ShouldBeFalse();
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
        Thread.Sleep(10); // Ensure time difference
        auditState.SetUpdated("Updater", "Update action");
        Thread.Sleep(10); // Ensure time difference
        auditState.SetDeactivated("Deactivator", "Deactivation action");

        // Act
        var lastActionDate = auditState.LastActionDate;

        // Assert
        lastActionDate.ShouldNotBeNull();
        lastActionDate.Value.Ticks.ShouldBe(auditState.DeactivatedDate.Value.Ticks);
    }

    [Fact]
    public void LastActionDate_WhenActivationPerformed_ShouldReturnActivationDate()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetCreated("Creator", "Initial creation");
        Thread.Sleep(10); // Ensure time difference
        auditState.SetDeactivated("Deactivator", "Deactivation action");
        Thread.Sleep(10); // Ensure time difference
        auditState.SetActivated("Activator", "Activation action");

        // Act
        var lastActionDate = auditState.LastActionDate;

        // Assert
        lastActionDate.ShouldNotBeNull();
        lastActionDate.Value.Ticks.ShouldBe(auditState.UpdatedDate.Value.Ticks);
    }

    [Fact]
    public void LastActionDate_WhenUndeletionPerformed_ShouldReturnUndeletionDate()
    {
        // Arrange
        var auditState = new AuditState();
        auditState.SetCreated("Creator", "Initial creation");
        Thread.Sleep(10); // Ensure time difference
        auditState.SetDeleted("Deleter", "Deletion action");
        Thread.Sleep(10); // Ensure time difference
        auditState.SetUndeleted("Restorer", "Restoration action");

        // Act
        var lastActionDate = auditState.LastActionDate;

        // Assert
        lastActionDate.ShouldNotBeNull();
        lastActionDate.Value.Ticks.ShouldBe(auditState.UpdatedDate.Value.Ticks);
    }
}