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
public class RequesterCodeGenGeneratorTests
{
    [Fact]
    public void Generator_NonPartialRequest_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Command]
public class InvalidCommand : RequestBase<Unit>
{
    [Handle]
    private Result<Unit> Handle() => Result<Unit>.Success();
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "RQGEN001").ShouldBeTrue();
    }

    [Fact]
    public void Generator_MissingHandleMethod_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Command]
public partial class InvalidCommand
{
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "RQGEN008").ShouldBeTrue();
    }

    [Fact]
    public void Generator_QueryWithUnitResponse_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Query]
public partial class InvalidQuery
{
    [Handle]
    private Result<Unit> Handle() => Result<Unit>.Success();
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "RQGEN004").ShouldBeTrue();
    }

    [Fact]
    public void Generator_ExplicitResponseTypeMismatch_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Query(typeof(string))]
public partial class InvalidQuery
{
    [Handle]
    private Result<int> Handle() => Result<int>.Success(42);
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "RQGEN021").ShouldBeTrue();
    }

    [Fact]
    public void Generator_StaticHandleMethod_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;

[Command]
public partial class InvalidCommand
{
    [Handle]
    private static Result<Unit> Handle() => Result<Unit>.Success();
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "RQGEN010").ShouldBeTrue();
    }

    [Fact]
    public void Generator_InvalidValidateSignature_ReportsDiagnostic()
    {
        const string source = """
using BridgingIT.DevKit.Common;
using FluentValidation;

[Command]
public partial class InvalidCommand
{
    [Validate]
    private static int Validate(InlineValidator<InvalidCommand> validator) => 42;

    [Handle]
    private Result<Unit> Handle() => Result<Unit>.Success();
}
""";

        var result = RunGenerator(source);

        result.Diagnostics.Any(d => d.Id == "RQGEN017").ShouldBeTrue();
    }

    [Fact]
    public void Generator_ValidQuery_EmitsHandlerHelpersValidatorAndPolicyAttributes()
    {
        const string source = """
using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using FluentValidation;

namespace TestNamespace;

public sealed class User
{
    public Guid UserId { get; set; }
}

public interface IUserRepository
{
    Task<User> FindAsync(Guid userId, CancellationToken cancellationToken);
}

[Query]
[HandlerRetry(2, 100)]
[HandlerTimeout(50)]
public partial class GetUserQuery
{
    public Guid UserId { get; set; }

    [Validate]
    private static void Validate(InlineValidator<GetUserQuery> validator)
    {
        validator.RuleFor(x => x.UserId).NotEmpty();
    }

    [Handle]
    private async Task<Result<User>> HandleAsync(
        IUserRepository repository,
        CancellationToken cancellationToken)
    {
        var user = await repository.FindAsync(UserId, cancellationToken);
        return user != null
            ? Success(user)
            : Failure($"User with ID {UserId} not found.");
    }
}
""";

        var result = RunGenerator(source);
        var generatedSource = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(sourceResult => sourceResult.SourceText.ToString()));

        result.Diagnostics.ShouldBeEmpty();
        result.CompilationDiagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generatedSource.ShouldContain("public partial class GetUserQuery : global::BridgingIT.DevKit.Common.RequestBase<global::TestNamespace.User>");
        generatedSource.ShouldContain("GetUserQueryGeneratedHandler");
        generatedSource.ShouldContain("[global::BridgingIT.DevKit.Common.HandlerRetryAttribute(2, 100)]");
        generatedSource.ShouldContain("[global::BridgingIT.DevKit.Common.HandlerTimeoutAttribute(50)]");
        generatedSource.ShouldContain("private static global::BridgingIT.DevKit.Common.Result<global::TestNamespace.User> Success(global::TestNamespace.User value)");
        generatedSource.ShouldContain("public sealed class Validator : global::FluentValidation.AbstractValidator<global::TestNamespace.GetUserQuery>");
        generatedSource.ShouldContain("/// Represents the generated Requester handler for");
        generatedSource.ShouldContain("return request.HandleAsync(repository, cancellationToken);");
    }

    [Fact]
    public void Generator_CommandWithoutExplicitResponse_InfersTypedResponse()
    {
        const string source = """
using BridgingIT.DevKit.Common;

namespace TestNamespace;

public sealed class CreateUserResult
{
    public string Username { get; set; }
}

[Command]
public partial class CreateUserCommand
{
    public string Username { get; set; }

    [Handle]
    private Result<CreateUserResult> Handle()
    {
        return Success(new CreateUserResult { Username = Username });
    }
}
""";

        var result = RunGenerator(source);
        var generatedSource = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(sourceResult => sourceResult.SourceText.ToString()));

        result.Diagnostics.ShouldBeEmpty();
        result.CompilationDiagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generatedSource.ShouldContain("public partial class CreateUserCommand : global::BridgingIT.DevKit.Common.RequestBase<global::TestNamespace.CreateUserResult>");
        generatedSource.ShouldContain("private static global::BridgingIT.DevKit.Common.Result<global::TestNamespace.CreateUserResult> Success(global::TestNamespace.CreateUserResult value)");
    }

    private static GeneratorRunData RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new RequesterSourceGenerator());
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
            "RequesterCodeGenTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed record GeneratorRunData(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<Diagnostic> CompilationDiagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);
}
