// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

[UnitTest("Domain")]
public class TypedEntityIdClassGeneratorTests
{
    [Fact]
    public void GenerateIdClass_ForValidEntityWithGuid_ShouldGenerateCorrectCode()
    {
        // Arrange
        const string source = @"
using System;
using BridgingIT.DevKit.Domain;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class TypedEntityIdAttribute<TId> : Attribute { }

public interface IEntity { }

[TypedEntityId<Guid>]
public class User : IEntity
{
    public string Name { get; set; }
}
";
        var (compilation, generator) = this.GetGeneratorAndCompilation(source);

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        // Assert
        var result = driver.GetRunResult().Results.Single();
        var generatedCode = result.GeneratedSources.Single().SourceText.ToString();
        Assert.NotEmpty(result.GeneratedSources);

        Assert.Contains("public partial class UserId : EntityId<Guid>", generatedCode);
        Assert.Contains("public static UserId Create()", generatedCode);
        Assert.Contains("public static UserId Create(Guid id)", generatedCode);
        Assert.Contains("public static UserId Create(string id)", generatedCode);
        Assert.Contains("public static implicit operator Guid(UserId id) => id?.Value ?? default;", generatedCode);
        Assert.Contains("public static implicit operator string(UserId id) => id?.Value.ToString();", generatedCode);
        Assert.Contains("public static implicit operator UserId(Guid id) => Create(id);", generatedCode);
    }

    [Fact]
    public void GenerateIdClass_ForValidEntityWithInt_ShouldGenerateCorrectCode()
    {
        // Arrange
        const string source = @"
using System;
using BridgingIT.DevKit.Domain;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class TypedEntityIdAttribute<TId> : Attribute { }

public interface IEntity { }

[TypedEntityId<int>]
public class Order : IEntity
{
    public decimal Amount { get; set; }
}
";
        var (compilation, generator) = this.GetGeneratorAndCompilation(source);

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        // Assert
        var result = driver.GetRunResult().Results.Single();
        var generatedCode = result.GeneratedSources.Single().SourceText.ToString();

        Assert.NotEmpty(result.GeneratedSources);
        Assert.Contains("public partial class OrderId : EntityId<Int32>", generatedCode);
        Assert.Contains("public static OrderId Create(Int32 id)", generatedCode);
        Assert.Contains("public static OrderId Create(string id)", generatedCode);
        Assert.Contains("public static implicit operator Int32(OrderId id) => id?.Value ?? default;", generatedCode);
        Assert.Contains("public static implicit operator string(OrderId id) => id?.Value.ToString();", generatedCode);
        Assert.Contains("public static implicit operator OrderId(Int32 id) => Create(id);", generatedCode);
    }

    [Fact]
    public void GenerateIdClass_ForNonEntityClass_ShouldGenerateId()
    {
        // Arrange
        const string source = @"
using System;
using BridgingIT.DevKit.Domain;

[TypedEntityId<Guid>]
public partial class ProductId
{
}
";
        var (compilation, generator) = this.GetGeneratorAndCompilation(source);

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        // Assert
        var result = driver.GetRunResult()
            .Results.Single();
        var generatedCode = result.GeneratedSources.Single()
            .SourceText.ToString();

        Assert.NotEmpty(result.GeneratedSources);
        Assert.Contains("public partial class ProductId : EntityId<Guid>", generatedCode);
    }

    [Fact]
    public void GenerateIdClass_ForIndirectEntityImplementation_ShouldGenerateCorrectCode()
    {
        // Arrange
        const string source = @"
using System;
using BridgingIT.DevKit.Domain;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class TypedEntityIdAttribute<TId> : Attribute { }

public interface IEntity { }
public abstract class BaseEntity : IEntity { }

[TypedEntityId<Guid>]
public class Product : BaseEntity
{
    public string Name { get; set; }
}
";
        var (compilation, generator) = this.GetGeneratorAndCompilation(source);

        // Act
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        // Assert
        var result = driver.GetRunResult().Results.Single();
        var generatedCode = result.GeneratedSources.Single().SourceText.ToString();

        Assert.Contains("public partial class ProductId : EntityId<Guid>", generatedCode);
        Assert.Contains("public static ProductId Create()", generatedCode);
        Assert.Contains("public static ProductId Create(Guid id)", generatedCode);
        Assert.Contains("public static ProductId Create(string id)", generatedCode);
    }

    private (Compilation, ISourceGenerator) GetGeneratorAndCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TypedEntityIdClassGenerator).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create("TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypedEntityIdClassGenerator();

        return (compilation, generator);
    }
}