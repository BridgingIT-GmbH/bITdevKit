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
public class PipelineCodeGenGeneratorTests
{
    [Fact]
    public void Generator_NonPartialPipeline_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Pipeline]
public class InvalidGeneratedPipeline : PipelineDefinition
{
    [PipelineStep(10)]
    public void ExecuteStep() { }
}
""";

        var diagnostics = RunGenerator(source);

        diagnostics.Any(d => d.Id == "PLNGEN001").ShouldBeTrue();
    }

    [Fact]
    public void Generator_DuplicateStepOrder_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Pipeline]
public partial class InvalidGeneratedPipeline : PipelineDefinition
{
    [PipelineStep(10)]
    public void StepOne() { }

    [PipelineStep(10)]
    public void StepTwo() { }
}
""";

        var diagnostics = RunGenerator(source);

        diagnostics.Any(d => d.Id == "PLNGEN009").ShouldBeTrue();
    }

    [Fact]
    public void Generator_UnsupportedReturnType_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

public class SampleContext : PipelineContextBase
{
}

[Pipeline(typeof(SampleContext))]
public partial class InvalidGeneratedPipeline : PipelineDefinition<SampleContext>
{
    [PipelineStep(10)]
    public int InvalidStep(SampleContext context) => 42;
}
""";

        var diagnostics = RunGenerator(source);

        diagnostics.Any(d => d.Id == "PLNGEN007").ShouldBeTrue();
    }

    private static ImmutableArray<Diagnostic> RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new PipelineSourceGenerator());
        driver = driver.RunGenerators(compilation);

        return driver.GetRunResult().Results.Single().Diagnostics;
    }

    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => a.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => MetadataReference.CreateFromFile(location))
            .Cast<MetadataReference>();

        return CSharpCompilation.Create(
            "PipelineCodeGenTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
