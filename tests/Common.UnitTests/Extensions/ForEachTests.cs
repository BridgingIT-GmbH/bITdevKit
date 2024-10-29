// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using NSubstitute;
using Shouldly;
using Xunit;

public class ForEachTests
{
    private readonly Faker faker = new();

    [Fact]
    public void ForEach_WithValidEnumerable_AppliesActionToAllItems()
    {
        // Arrange
        var items = new List<string> { this.faker.Lorem.Word(), this.faker.Lorem.Word() };
        var processedItems = new List<string>();

        // Act
        items.ForEach(item => processedItems.Add(item));

        // Assert
        processedItems.Count.ShouldBe(items.Count);
        processedItems.ShouldBe(items);
    }

    [Fact]
    public void ForEach_WithNullSource_ReturnsSourceWithoutException()
    {
        // Arrange
        IEnumerable<string> items = null;
        var action = Substitute.For<Action<string>>();

        // Act
        var result = items.ForEach(action);

        // Assert
        result.ShouldBe(items);
        action.DidNotReceiveWithAnyArgs().Invoke(default);
    }

    [Fact]
    public void ForEach_WithNullAction_ReturnsSourceWithoutException()
    {
        // Arrange
        var items = new List<string> { this.faker.Lorem.Word() }.AsEnumerable();
        Action<string> action = null;

        // Act
        var result = items.ForEach(action);

        // Assert
        result.ShouldBe(items);
    }

    [Fact]
    public void ForEach_WithCancellationToken_CancelsOperation()
    {
        // Arrange
        var items = new List<string> { this.faker.Lorem.Word(), this.faker.Lorem.Word() };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Should.Throw<OperationCanceledException>(() =>
            items.ForEach(
                _ => { },
                cts.Token));
    }

    [Fact]
    public async Task ForEachAsync_WithValidEnumerable_AppliesActionToAllItems()
    {
        // Arrange
        var items = new List<string> { this.faker.Lorem.Word(), this.faker.Lorem.Word() };
        var processedItems = new List<string>();

        // Act
        await items.ForEachAsync(item =>
        {
            processedItems.Add(item);
            return Task.CompletedTask;
        });

        // Assert
        processedItems.Count.ShouldBe(items.Count);
        processedItems.ShouldBe(items);
    }

    [Fact]
    public async Task ForEachAsync_WithNullSource_ReturnsSourceWithoutException()
    {
        // Arrange
        IEnumerable<string> items = null;
        var action = Substitute.For<Func<string, Task>>();

        // Act
        var result = await items.ForEachAsync(action);

        // Assert
        result.ShouldBe(items);
        await action.DidNotReceiveWithAnyArgs().Invoke(default);
    }

    [Fact]
    public async Task ForEachParallelAsync_ProcessesItemsInParallel()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).Select(_ => this.faker.Lorem.Word()).ToList();
        var processedItems = new List<string>();
        var lockObj = new object();

        // Act
        await items.ForEachParallelAsync(
            item =>
            {
                lock (lockObj)
                {
                    processedItems.Add(item);
                }
                return Task.CompletedTask;
            },
            maxDegreeOfParallelism: 2,
            batchSize: 3);

        // Assert
        processedItems.Count.ShouldBe(items.Count);
        processedItems.ToHashSet().SetEquals(items.ToHashSet()).ShouldBeTrue();
    }

    [Fact]
    public async Task ForEachParallelAsync_WithInvalidBatchSize_ThrowsArgumentException()
    {
        // Arrange
        var items = new List<string> { this.faker.Lorem.Word() };

        // Act & Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            items.ForEachParallelAsync(
                _ => Task.CompletedTask,
                maxDegreeOfParallelism: 1,
                batchSize: 0));
    }

    [Fact]
    public void ForEach_WithChildSelector_ProcessesNestedStructures()
    {
        // Arrange
        var processedItems = new List<string>();
        var rootItems = new List<TreeNode>
        {
            new(this.faker.Lorem.Word())
            {
                Children =
                [
                    new(this.faker.Lorem.Word()),
                    new(this.faker.Lorem.Word())
                ]
            }
        };

        // Act
        rootItems.ForEach(
            node => node.Children,
            node => processedItems.Add(node.Value));

        // Assert
        processedItems.Count.ShouldBe(3);
    }

    private class TreeNode(string value)
    {
        public string Value { get; } = value;

        public List<TreeNode> Children { get; set; } = [];
    }
}