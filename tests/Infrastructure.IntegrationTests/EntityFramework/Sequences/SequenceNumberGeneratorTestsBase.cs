// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Sequences;

using BridgingIT.DevKit.Domain.Repositories;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

[IntegrationTest("Infrastructure")]
public abstract class SequenceNumberGeneratorTestsBase
{
    protected IServiceProvider ServiceProvider { get; set; }

    protected abstract ISequenceNumberGenerator CreateGenerator();

    [Fact]
    public virtual async Task GetNextAsync_SequenceExists_ReturnsNextValue()
    {
        // Arrange
        var sut = this.CreateGenerator();
        const string sequenceName = "TestSequence";

        // Act
        var result1 = await sut.GetNextAsync(sequenceName);
        var result2 = await sut.GetNextAsync(sequenceName);

        // Assert
        result1.ShouldBeSuccess();
        result2.ShouldBeSuccess();
        result2.Value.ShouldBeGreaterThan(result1.Value);
    }

    [Fact]
    public virtual async Task GetNextAsync_SequenceDoesNotExist_ReturnsFailureWithNotFoundError()
    {
        // Arrange
        var sut = this.CreateGenerator();
        const string sequenceName = "NonExistentSequence";

        // Act
        var result = await sut.GetNextAsync(sequenceName);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.First().ShouldBeOfType<SequenceNotFoundError>();
    }

    [Fact]
    public virtual async Task GetSequenceInfoAsync_SequenceExists_ReturnsInfo()
    {
        // Arrange
        var sut = this.CreateGenerator();
        const string sequenceName = "TestSequence";

        // Act
        var result = await sut.GetSequenceInfoAsync(sequenceName);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Name.ShouldBe(sequenceName);
    }

    [Fact]
    public virtual async Task ResetSequenceAsync_SequenceExists_ResetsSuccessfully()
    {
        // Arrange
        var sut = this.CreateGenerator();
        const string sequenceName = "TestSequence";
        await sut.GetNextAsync(sequenceName);  // Increment once

        // Act
        var resetResult = await sut.ResetSequenceAsync(sequenceName, 1);
        var nextResult = await sut.GetNextAsync(sequenceName);

        // Assert
        resetResult.ShouldBeSuccess();
        nextResult.ShouldBeSuccess();
        nextResult.Value.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public virtual async Task GetNextMultipleAsync_MultipleSequences_ReturnsValues()
    {
        // Arrange
        var sut = this.CreateGenerator();
        var sequences = new[] { "OtherTestSequence1", "OtherTestSequence2" };

        // Act
        var result = await sut.GetNextMultipleAsync(sequences);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Count.ShouldBe(2);
        result.Value.Keys.ShouldContain(sequences[0]);
        result.Value.Keys.ShouldContain(sequences[1]);
        result.Value.Values.All(v => v > 0).ShouldBeTrue();
    }
}
