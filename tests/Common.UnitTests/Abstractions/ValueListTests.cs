// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

[UnitTest("Common")]
public class ValueListTests
{
    private readonly Faker faker = new();

    public class StringTests
    {
        private readonly Faker faker = new();

        [Fact]
        public void Add_WhenEmpty_ShouldAddSingleItem()
        {
            // Arrange
            var sut = new ValueList<string>();
            var item = this.faker.Lorem.Word();

            // Act
            var result = sut.Add(item);

            // Assert
            result.Count.ShouldBe(1);
            result.IsEmpty.ShouldBeFalse();
            result.AsEnumerable().ShouldContain(item);
        }

        [Fact]
        public void Add_WhenOneItem_ShouldAddSecondItem()
        {
            // Arrange
            var firstItem = this.faker.Lorem.Word();
            var secondItem = this.faker.Lorem.Word();
            var sut = new ValueList<string>().Add(firstItem);

            // Act
            var result = sut.Add(secondItem);

            // Assert
            result.Count.ShouldBe(2);
            result.AsEnumerable().ShouldContain(firstItem);
            result.AsEnumerable().ShouldContain(secondItem);
        }

        [Fact]
        public void Add_WhenTwoItems_ShouldMoveToOverflow()
        {
            // Arrange
            var items = new[] { this.faker.Lorem.Word(), this.faker.Lorem.Word() };
            var thirdItem = this.faker.Lorem.Word();
            var sut = new ValueList<string>()
                .Add(items[0])
                .Add(items[1]);

            // Act
            var result = sut.Add(thirdItem);

            // Assert
            result.Count.ShouldBe(3);
            result.AsEnumerable().ShouldContain(items[0]);
            result.AsEnumerable().ShouldContain(items[1]);
            result.AsEnumerable().ShouldContain(thirdItem);
        }

        [Fact]
        public void AddRange_WithNullCollection_ShouldReturnSameInstance()
        {
            // Arrange
            var sut = new ValueList<string>();

            // Act
            var result = sut.AddRange(null);

            // Assert
            result.ShouldBe(sut);
        }

        [Fact]
        public void AddRange_WithMultipleItems_ShouldAddAllItems()
        {
            // Arrange
            var sut = new ValueList<string>();
            var items = new[]
            {
                this.faker.Lorem.Word(),
                this.faker.Lorem.Word(),
                this.faker.Lorem.Word()
            };

            // Act
            var result = sut.AddRange(items);

            // Assert
            result.Count.ShouldBe(3);
            foreach (var item in items)
            {
                result.AsEnumerable().ShouldContain(item);
            }
        }
    }

    public class ErrorTests
    {
        private readonly Faker faker = new();

        [Fact]
        public void Add_WhenEmpty_ShouldAddSingleError()
        {
            // Arrange
            var sut = new ValueList<Error>();
            var error = new Error(this.faker.Lorem.Sentence());

            // Act
            var result = sut.Add(error);

            // Assert
            result.Count.ShouldBe(1);
            result.IsEmpty.ShouldBeFalse();
            result.AsEnumerable().ShouldContain(error);
        }

        [Fact]
        public void Add_WhenOneError_ShouldAddSecondError()
        {
            // Arrange
            var firstError = new Error(this.faker.Lorem.Sentence());
            var secondError = new Error(this.faker.Lorem.Sentence());
            var sut = new ValueList<Error>().Add(firstError);

            // Act
            var result = sut.Add(secondError);

            // Assert
            result.Count.ShouldBe(2);
            result.AsEnumerable().ShouldContain(firstError);
            result.AsEnumerable().ShouldContain(secondError);
        }

        [Fact]
        public void AddRange_WithMultipleErrors_ShouldAddAllErrors()
        {
            // Arrange
            var sut = new ValueList<Error>();
            var errors = new[]
            {
                new Error(this.faker.Lorem.Sentence()),
                new Error(this.faker.Lorem.Sentence()),
                new Error(this.faker.Lorem.Sentence())
            };

            // Act
            var result = sut.AddRange(errors);

            // Assert
            result.Count.ShouldBe(3);
            foreach (var error in errors)
            {
                result.AsEnumerable().ShouldContain(error);
            }
        }

        [Fact]
        public void AsEnumerable_WhenEmpty_ShouldReturnEmptyCollection()
        {
            // Arrange
            var sut = new ValueList<Error>();

            // Act
            var result = sut.AsEnumerable();

            // Assert
            result.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// To run the benchmarks: console project -> BenchmarkRunner.Run<ValueListPerfTests>();
    /// </summary>
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class ValueListPerfTests
    {
        private const int SmallSize = 2;
        private const int MediumSize = 5;
        private readonly string[] smallData;
        private readonly string[] mediumData;

        public ValueListPerfTests()
        {
            var faker = new Faker();
            this.smallData = Enumerable.Range(0, SmallSize)
                .Select(_ => faker.Lorem.Word())
                .ToArray();

            this.mediumData = Enumerable.Range(0, MediumSize)
                .Select(_ => faker.Lorem.Word())
                .ToArray();
        }

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Small")]
        public void List_Small()
        {
            var list = new List<string>();
            foreach (var item in this.smallData)
            {
                list.Add(item);
            }
        }

        [Benchmark]
        [BenchmarkCategory("Small")]
        public void ValueList_Small()
        {
            var list = new ValueList<string>();
            foreach (var item in this.smallData)
            {
                list = list.Add(item);
            }
        }

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Medium")]
        public void List_Medium()
        {
            var list = new List<string>();
            foreach (var item in this.mediumData)
            {
                list.Add(item);
            }
        }

        [Benchmark]
        [BenchmarkCategory("Medium")]
        public void ValueList_Medium()
        {
            var list = new ValueList<string>();
            foreach (var item in this.mediumData)
            {
                list = list.Add(item);
            }
        }
    }

    // [SimpleJob(RuntimeMoniker.Net80)]
    // [MemoryDiagnoser]
    // public class ValueListAllocationTests
    // {
    //     private readonly Faker faker = new();
    //
    //     [Fact]
    //     public void Add_SingleItem_ShouldNotAllocateOnHeap()
    //     {
    //         // Arrange
    //         var item = this.faker.Lorem.Word();
    //         var initialAllocated = GC.GetTotalAllocatedBytes(true);
    //
    //         // Act
    //         var sut = new ValueList<string>();
    //         sut = sut.Add(item);
    //         var finalAllocated = GC.GetTotalAllocatedBytes(true);
    //
    //         // Assert
    //         var allocated = finalAllocated - initialAllocated;
    //         allocated.ShouldBeLessThan(150); // Allow for small overhead
    //     }
    //
    //     [Fact]
    //     public void Add_TwoItems_ShouldNotAllocateOnHeap()
    //     {
    //         // Arrange
    //         var items = new[] { this.faker.Lorem.Word(), this.faker.Lorem.Word() };
    //         var initialAllocated = GC.GetTotalAllocatedBytes(true);
    //
    //         // Act
    //         var sut = new ValueList<string>();
    //         sut = sut.Add(items[0]).Add(items[1]);
    //         var finalAllocated = GC.GetTotalAllocatedBytes(true);
    //
    //         // Assert
    //         var allocated = finalAllocated - initialAllocated;
    //         allocated.ShouldBeLessThan(150); // Allow for small overhead
    //     }
    //
    //     [Fact]
    //     public void Add_ThreeItems_ShouldAllocateOnHeap()
    //     {
    //         // Arrange
    //         var items = new[]
    //         {
    //             this.faker.Lorem.Word(),
    //             this.faker.Lorem.Word(),
    //             this.faker.Lorem.Word()
    //         };
    //         var initialAllocated = GC.GetTotalAllocatedBytes(true);
    //
    //         // Act
    //         var sut = new ValueList<string>();
    //         sut = sut.Add(items[0]).Add(items[1]).Add(items[2]);
    //         var finalAllocated = GC.GetTotalAllocatedBytes(true);
    //
    //         // Assert
    //         var allocated = finalAllocated - initialAllocated;
    //         allocated.ShouldBeGreaterThan(150); // Should allocate for List<T>
    //     }
    // }
}