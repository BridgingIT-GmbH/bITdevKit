// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

[UnitTest("Common")]
public class PipelineContextValidationGeneratorTests
{
    [Fact]
    public void Generator_NonPartialContext_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

public sealed class InvalidContext : PipelineContextBase
{
    [ValidateNotEmpty]
    public string SourceFileName { get; set; }
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "PLNVAL001").ShouldBeTrue();
    }

    [Fact]
    public void Generator_InvalidValidateSignature_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;
using FluentValidation;

public sealed partial class InvalidContext : PipelineContextBase
{
    [Validate]
    private static int Validate(InlineValidator<InvalidContext> validator) => 42;
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "PLNVAL004").ShouldBeTrue();
    }

    [Fact]
    public void Generator_PropertyValidationAttributesAndValidateMethod_AreMergedIntoGeneratedValidator()
    {
        const string source = """
using System.Collections.Generic;
using BridgingIT.DevKit.Common;
using FluentValidation;

namespace TestNamespace;

public sealed partial class OrderImportContext : PipelineContextBase
{
    [ValidateNotEmpty("Source file name is required.")]
    public string SourceFileName { get; set; }

    [ValidateEachNotEmpty]
    public List<string> Tags { get; set; }

    [Validate]
    private static void Validate(InlineValidator<OrderImportContext> validator)
    {
        validator.RuleFor(x => x.SourceFileName).MinimumLength(3);
    }
}
""";

        var result = RunGenerator(source);
        var generatedSource = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(static sourceResult => sourceResult.SourceText.ToString()));

        result.Diagnostics.ShouldBeEmpty();
        result.CompilationDiagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generatedSource.ShouldContain("public sealed partial class OrderImportContext");
        generatedSource.ShouldContain("public sealed class Validator : global::FluentValidation.AbstractValidator<global::TestNamespace.OrderImportContext>");
        generatedSource.ShouldContain("this.RuleFor(x => x.SourceFileName)");
        generatedSource.ShouldContain(".WithMessage(\"Source file name is required.\")");
        generatedSource.ShouldContain("this.RuleForEach(x => x.Tags)");
        generatedSource.ShouldContain("var validator = new global::FluentValidation.InlineValidator<global::TestNamespace.OrderImportContext>();");
        generatedSource.ShouldContain("global::TestNamespace.OrderImportContext.Validate(validator);");
        generatedSource.ShouldContain("this.Include(validator);");
    }

    [Fact]
    public void Generator_ValidateEachOnScalarProperty_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

public sealed partial class InvalidContext : PipelineContextBase
{
    [ValidateEachNotEmpty]
    public string SourceFileName { get; set; }
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "PLNVAL012").ShouldBeTrue();
    }

    [Fact]
    public void Generator_NumericComparisonOnUnsupportedType_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

public sealed partial class InvalidContext : PipelineContextBase
{
    [ValidateGreaterThan(0)]
    public object Value { get; set; }
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "PLNVAL010").ShouldBeTrue();
    }

    private static GeneratorRunData RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new PipelineContextValidationSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var runResult = driver.GetRunResult();

        return new GeneratorRunData(
            runResult.Results.Single().Diagnostics,
            outputCompilation.GetDiagnostics(),
            runResult.Results.Single().GeneratedSources);
    }

    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => MetadataReference.CreateFromFile(location))
            .Cast<MetadataReference>();

        return CSharpCompilation.Create(
            "PipelineContextValidationGeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed record GeneratorRunData(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<Diagnostic> CompilationDiagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);
}
