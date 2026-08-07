// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using BridgingIT.DevKit.Domain.Model;

public class ChangeHistoryOptionsTests
{
    [Fact]
    public void Track_EntityImplementingConcurrency_ExcludesConcurrencyVersion()
    {
        // Arrange
        var options = new ChangeHistoryOptions();

        // Act
        var sut = options.Track<ConcurrencyEntity>().EntityOptions;

        // Assert
        sut.PropertyPolicies[nameof(IConcurrency.ConcurrencyVersion)]
            .ShouldBe(ChangeHistoryValuePolicy.Exclude);
    }

    [Fact]
    public void Track_ExplicitConcurrencyPolicy_OverridesDefaultExclusion()
    {
        // Arrange
        var options = new ChangeHistoryOptions();

        // Act
        var sut = options.Track<ConcurrencyEntity>()
            .HashOnly(entity => entity.ConcurrencyVersion)
            .EntityOptions;

        // Assert
        sut.PropertyPolicies[nameof(IConcurrency.ConcurrencyVersion)]
            .ShouldBe(ChangeHistoryValuePolicy.HashOnly);
    }

    [Fact]
    public void Track_EntityWithoutConcurrency_DoesNotAddConventionPolicy()
    {
        // Arrange
        var options = new ChangeHistoryOptions();

        // Act
        var sut = options.Track<StubEntity>().EntityOptions;

        // Assert
        sut.PropertyPolicies.ShouldBeEmpty();
    }

    [Fact]
    public void CaptureChanges_DefaultConfiguration_EnablesStandardCaptureSources()
    {
        // Arrange
        var options = new ChangeHistoryOptions()
            .UseCaptureStrategy(ChangeHistoryCaptureStrategy.EntityChangeOnly);

        // Act
        var sut = options.Track<StubEntity>()
            .CaptureChanges()
            .EntityOptions;

        // Assert
        sut.CaptureCreates.ShouldBeTrue();
        sut.CaptureDirectMutations.ShouldBeTrue();
        sut.DirectMutationMode.ShouldBe(ChangeHistoryCaptureMode.Required);
        sut.CaptureUpdateSet.ShouldBeTrue();
        sut.UpdateSetMode.ShouldBe(ChangeHistoryCaptureMode.BestEffort);
        sut.UpdateSetMaxAffectedRows.ShouldBeNull();
        sut.CaptureStrategy.ShouldBe(ChangeHistoryCaptureStrategy.EntityChangeOnly);
    }

    [Fact]
    public void CaptureChanges_CustomConfiguration_AppliesCaptureModesAndLimit()
    {
        // Arrange
        var options = new ChangeHistoryOptions();

        // Act
        var sut = options.Track<StubEntity>()
            .CaptureChanges(
                ChangeHistoryCaptureMode.BestEffort,
                ChangeHistoryCaptureMode.Required,
                updateSetMaxAffectedRows: 250)
            .EntityOptions;

        // Assert
        sut.DirectMutationMode.ShouldBe(ChangeHistoryCaptureMode.BestEffort);
        sut.UpdateSetMode.ShouldBe(ChangeHistoryCaptureMode.Required);
        sut.UpdateSetMaxAffectedRows.ShouldBe(250);
    }

    [Fact]
    public void CaptureBulkInserts_DefaultConfiguration_EnablesSummaryCapture()
    {
        // Arrange
        var options = new ChangeHistoryOptions();

        // Act
        var sut = options.Track<StubEntity>()
            .CaptureBulkInserts()
            .EntityOptions;

        // Assert
        sut.BulkInsertCaptureMode.ShouldBe(ChangeHistoryBulkInsertCaptureMode.Summary);
        sut.BulkInsertMaxDetailedEntities.ShouldBe(1000);
    }

    [Fact]
    public void Validate_DetailedBulkInsertCaptureWithoutPositiveLimit_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ChangeHistoryOptions();
        options.Track<StubEntity>()
            .CaptureBulkInserts(
                ChangeHistoryBulkInsertCaptureMode.Detailed,
                maxDetailedEntities: 0);

        // Act
        var action = options.Validate;

        // Assert
        action.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("requires a maximum entity limit greater than zero");
    }

    [Fact]
    public void AllowRestoreUsingValidatedSetters_AnonymousProjection_ConfiguresEveryProperty()
    {
        // Arrange
        var options = new ChangeHistoryOptions();

        // Act
        var sut = options.Track<StubEntity>()
            .AllowRestoreUsingValidatedSetters(entity => new
            {
                entity.Name,
                entity.Description,
                entity.Priority
            })
            .EntityOptions;

        // Assert
        sut.RestorePolicies.Keys.ShouldBe([
            nameof(StubEntity.Name),
            nameof(StubEntity.Description),
            nameof(StubEntity.Priority)]);
        sut.RestorePolicies.Values.ShouldAllBe(
            policy => policy.ExecutionMode == ChangeHistoryRestoreExecutionMode.ValidatedSetter &&
                      policy.HandlerName == ChangeHistoryRestoreExecutionMode.ValidatedSetter.ToString());
    }

    [Fact]
    public void AllowRestoreUsingValidatedSetters_SingleProperty_ConfiguresProperty()
    {
        // Arrange
        var options = new ChangeHistoryOptions();

        // Act
        var sut = options.Track<StubEntity>()
            .AllowRestoreUsingValidatedSetters(entity => entity.Name)
            .EntityOptions;

        // Assert
        var policy = sut.RestorePolicies.ShouldHaveSingleItem();
        policy.Key.ShouldBe(nameof(StubEntity.Name));
        policy.Value.ExecutionMode.ShouldBe(ChangeHistoryRestoreExecutionMode.ValidatedSetter);
    }

    [Fact]
    public void AllowRestoreUsingValidatedSetters_InvalidProjection_ThrowsArgumentException()
    {
        // Arrange
        var options = new ChangeHistoryOptions();
        var builder = options.Track<StubEntity>();

        // Act
        var action = () => builder.AllowRestoreUsingValidatedSetters(entity => new
        {
            entity.Name,
            Constant = 42
        });

        // Assert
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("properties");
        builder.EntityOptions.RestorePolicies.ShouldBeEmpty();
    }

    private class StubEntity : Entity<Guid>
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int Priority { get; set; }
    }

    private sealed class ConcurrencyEntity : StubEntity, IConcurrency
    {
        public Guid ConcurrencyVersion { get; set; }
    }
}