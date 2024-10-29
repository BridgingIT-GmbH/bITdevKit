// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using Bogus;
using Shouldly;
using Xunit;

public class ToHierarchyTests
{
    private readonly Faker faker = new();

    private class TestItem
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void ToHierarchy_WithValidData_CreatesHierarchicalStructure()
    {
        // Arrange
        var items = new[]
        {
            new TestItem { Id = 1, ParentId = null, Name = this.faker.Random.Word() },
            new TestItem { Id = 2, ParentId = 1, Name = this.faker.Random.Word() },
            new TestItem { Id = 3, ParentId = 1, Name = this.faker.Random.Word() },
            new TestItem { Id = 4, ParentId = 2, Name = this.faker.Random.Word() }
        };

        // Act
        var result = items.ToHierarchy(i => i.Id, i => i.ParentId).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Children.Count().ShouldBe(2);
        result[0].Children.First().Children.Count().ShouldBe(1);
    }

    [Fact]
    public void ToHierarchy_WithEmptySource_ReturnsEmptyCollection()
    {
        // Arrange
        var items = Array.Empty<TestItem>();

        // Act
        var result = items.ToHierarchy(i => i.Id, i => i.ParentId);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ToHierarchy_WithNullSelectors_ReturnsEmptyCollection()
    {
        // Arrange
        var items = new[] { new TestItem { Id = 1, Name = this.faker.Random.Word() } };

        // Act
        var result = items.ToHierarchy<TestItem, int>(null, null);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ToFlatten_SingleNode_ReturnsCorrectFlatStructure()
    {
        // Arrange
        var root = new HierarchyNode<TestItem>
        {
            Item = new TestItem { Id = 1, Name = this.faker.Random.Word() }
        };

        // Act
        var result = root.ToFlatten(
            node => node.Id,
            (item, parentId) => new TestItem { Id = item.Id, ParentId = parentId, Name = item.Name }
        ).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].ParentId.ShouldBeNull();
    }

    [Fact]
    public void ToFlatten_ComplexHierarchy_ReturnsCorrectFlatStructure()
    {
        // Arrange
        var root = new HierarchyNode<TestItem>
        {
            Item = new TestItem { Id = 1, Name = this.faker.Random.Word() },
            Children =
            [
                new HierarchyNode<TestItem>
                {
                    Item = new TestItem { Id = 2, Name = this.faker.Random.Word() },
                    Children =
                    [
                        new HierarchyNode<TestItem>
                        {
                            Item = new TestItem { Id = 4, Name = this.faker.Random.Word() }
                        }
                    ]
                },
                new HierarchyNode<TestItem>
                {
                    Item = new TestItem { Id = 3, Name = this.faker.Random.Word() }
                }
            ]
        };

        // Act
        var result = root.ToFlatten(
            node => node.Id,
            (item, parentId) => new TestItem { Id = item.Id, ParentId = parentId, Name = item.Name }
        ).ToList();

        // Assert
        result.Count.ShouldBe(4);
        result.Count(x => x.ParentId == null).ShouldBe(1);
        result.Count(x => x.ParentId == 1).ShouldBe(2);
        result.Count(x => x.ParentId == 2).ShouldBe(1);
    }

    [Fact]
    public void ToFlatten_MultipleRoots_ReturnsCorrectFlatStructure()
    {
        // Arrange
        var roots = new[]
        {
            new HierarchyNode<TestItem>
            {
                Item = new TestItem { Id = 1, Name = this.faker.Random.Word() },
                Children =
                [
                    new HierarchyNode<TestItem>
                    {
                        Item = new TestItem { Id = 2, Name = this.faker.Random.Word() }
                    }
                ]
            },
            new HierarchyNode<TestItem>
            {
                Item = new TestItem { Id = 3, Name = this.faker.Random.Word() }
            }
        };

        // Act
        var result = roots.ToFlatten(
            node => node.Id,
            (item, parentId) => new TestItem { Id = item.Id, ParentId = parentId, Name = item.Name }
        ).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result.Count(x => x.ParentId == null).ShouldBe(2);
        result.Count(x => x.ParentId == 1).ShouldBe(1);
    }

    [Fact]
    public void ToFlatten_EmptyRoots_ReturnsEmptyCollection()
    {
        // Arrange
        var roots = Array.Empty<HierarchyNode<TestItem>>();

        // Act
        var result = roots.ToFlatten(
            node => node.Id,
            (item, parentId) => new TestItem { Id = item.Id, ParentId = parentId, Name = item.Name }
        );

        // Assert
        result.ShouldBeEmpty();
    }
}