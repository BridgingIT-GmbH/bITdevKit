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
public class NotifierCodeGenGeneratorTests
{
    [Fact]
    public void Generator_NonPartialEvent_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Event]
public class InvalidEvent
{
    [Handle]
    private Result Handle() => Result.Success();
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "NTGEN001").ShouldBeTrue();
    }

    [Fact]
    public void Generator_MissingHandleMethod_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Event]
public partial class InvalidEvent
{
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "NTGEN005").ShouldBeTrue();
    }

    [Fact]
    public void Generator_InvalidHandleReturnType_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Event]
public partial class InvalidEvent
{
    [Handle]
    private int Handle() => 42;
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "NTGEN009").ShouldBeTrue();
    }

    [Fact]
    public void Generator_InvalidValidateSignature_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;
using FluentValidation;

[Event]
public partial class InvalidEvent
{
    [Validate]
    private static int Validate(InlineValidator<InvalidEvent> validator) => 42;

    [Handle]
    private Result Handle() => Result.Success();
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "NTGEN013").ShouldBeTrue();
    }

    [Fact]
    public void Generator_IncompatibleBaseType_ReportsDiagnostic()
    {
        const string source = """
using System;
using BridgingIT.DevKit.Common;

[Event]
public partial class InvalidEvent : Exception
{
    [Handle]
    private Result Handle() => Result.Success();
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "NTGEN002").ShouldBeTrue();
    }

    [Fact]
    public void Generator_OverloadedHandleMethods_ReportCollisionDiagnostic()
    {
        const string source = """
using System.Threading;
using BridgingIT.DevKit.Common;

[Event]
public partial class InvalidEvent
{
    [Handle]
    private Result Handle() => Result.Success();

    [Handle]
    private Result Handle(CancellationToken cancellationToken) => Result.Success();
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "NTGEN015").ShouldBeTrue();
    }

    [Fact]
    public void Generator_ValidEvent_EmitsMultipleHandlersHelpersValidatorAndPolicyAttributes()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using FluentValidation;

namespace TestNamespace;

public interface IEmailService
{
    Task SendAsync(string email, CancellationToken cancellationToken);
}

[Event]
[HandlerRetry(2, 300)]
[HandlerTimeout(50)]
public partial class UserRegisteredEvent
{
    [ValidateNotEmpty("Email is required.")]
    public string Email { get; set; }

    [Validate]
    private static void Validate(InlineValidator<UserRegisteredEvent> validator)
    {
        validator.RuleFor(x => x.Email).EmailAddress();
    }

    [Handle]
    private Result Audit()
    {
        return Success();
    }

    [Handle]
    private async Task<Result> SendEmailAsync(
        IEmailService emailService,
        CancellationToken cancellationToken)
    {
        await emailService.SendAsync(Email, cancellationToken);
        return Success();
    }
}
""";

        var result = RunGenerator(source);
        var generatedSource = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(sourceResult => sourceResult.SourceText.ToString()));

        result.Diagnostics.ShouldBeEmpty();
        result.CompilationDiagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generatedSource.ShouldContain("public partial class UserRegisteredEvent : global::BridgingIT.DevKit.Common.NotificationBase");
        generatedSource.ShouldContain("internal static global::System.Threading.Tasks.Task<global::BridgingIT.DevKit.Common.Result> __NotifierGeneratedInvoke_AuditAsync");
        generatedSource.ShouldContain("internal static global::System.Threading.Tasks.Task<global::BridgingIT.DevKit.Common.Result> __NotifierGeneratedInvoke_SendEmailAsyncAsync");
        generatedSource.ShouldContain("UserRegisteredEvent_AuditGeneratedHandler");
        generatedSource.ShouldContain("UserRegisteredEvent_SendEmailAsyncGeneratedHandler");
        generatedSource.ShouldContain("[global::BridgingIT.DevKit.Common.HandlerRetryAttribute(2, 100)]");
        generatedSource.ShouldContain("[global::BridgingIT.DevKit.Common.HandlerTimeoutAttribute(50)]");
        generatedSource.ShouldContain("private static global::BridgingIT.DevKit.Common.Result Success()");
        generatedSource.ShouldContain("public sealed class Validator : global::FluentValidation.AbstractValidator<global::TestNamespace.UserRegisteredEvent>");
        generatedSource.ShouldContain("global::TestNamespace.UserRegisteredEvent.Validate(validator);");
    }

    private static GeneratorRunData RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new NotifierSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var _);
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
            "NotifierCodeGenTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed record GeneratorRunData(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<Diagnostic> CompilationDiagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);
}
