// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#pragma warning disable RS1035
#pragma warning disable RS1036
#pragma warning disable RS1042
#pragma warning disable RS1038

/// <summary>
/// Generates packaged pipeline definition plumbing and wrapper steps for attributed pipeline classes.
/// </summary>
[Generator]
public class PipelineSourceGenerator : IIncrementalGenerator
{
    public const string PipelineAttributeName = "BridgingIT.DevKit.Common.PipelineAttribute";
    public const string PipelineStepAttributeName = "BridgingIT.DevKit.Common.PipelineStepAttribute";
    public const string PipelineHookAttributeName = "BridgingIT.DevKit.Common.PipelineHookAttribute";
    public const string PipelineBehaviorAttributeName = "BridgingIT.DevKit.Common.PipelineBehaviorAttribute";
    public const string PipelineDefinitionName = "BridgingIT.DevKit.Common.PipelineDefinition";
    public const string PipelineDefinitionOfTName = "BridgingIT.DevKit.Common.PipelineDefinition`1";
    public const string PipelineContextBaseName = "BridgingIT.DevKit.Common.PipelineContextBase";
    public const string NullPipelineContextName = "BridgingIT.DevKit.Common.NullPipelineContext";
    public const string ResultName = "BridgingIT.DevKit.Common.Result";
    public const string PipelineControlName = "BridgingIT.DevKit.Common.PipelineControl";
    public const string PipelineHookOfTName = "BridgingIT.DevKit.Common.IPipelineHook`1";
    public const string PipelineBehaviorOfTName = "BridgingIT.DevKit.Common.IPipelineBehavior`1";
    public const string CancellationTokenName = "System.Threading.CancellationToken";
    public const string TaskName = "System.Threading.Tasks.Task";
    public const string TaskOfTName = "System.Threading.Tasks.Task`1";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pipelineClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetPipelineCandidate(ctx))
            .Where(static symbol => symbol is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(pipelineClasses.Collect());

        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) =>
        {
            var (compilation, classSymbols) = source;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Partial declarations can surface multiple syntax hits for the same pipeline type.
            foreach (var classSymbol in classSymbols)
            {
                if (classSymbol is null)
                {
                    continue;
                }

                var key = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (!seen.Add(key))
                {
                    continue;
                }

                var model = PipelineGenerationModel.Create(spc, compilation, classSymbol);
                if (model is null)
                {
                    continue;
                }

                spc.AddSource(
                    $"{SanitizeHintName(model.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}.Pipeline.g.cs",
                    PipelineSourceEmitter.Emit(model));
            }
        });
    }

    private static INamedTypeSymbol GetPipelineCandidate(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        return classSymbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == PipelineAttributeName)
            ? classSymbol
            : null;
    }

    private static string SanitizeHintName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString();
    }
}

/// <summary>
/// Defines diagnostics produced by <see cref="PipelineSourceGenerator"/>.
/// </summary>
public static class PipelineSourceGeneratorDiagnostics
{
#pragma warning disable RS1032 // Define diagnostic message correctly
#pragma warning disable RS2008 // Enable analyzer release tracking
    public static readonly DiagnosticDescriptor PipelineMustBePartial = new(

        "PLNGEN001", "Pipeline must be partial",
        "Pipeline '{0}' must be declared as partial to use [Pipeline]",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor PipelineMustInheritPipelineDefinition = new(
        "PLNGEN002", "Pipeline must inherit PipelineDefinition",
        "Pipeline '{0}' must inherit PipelineDefinition or PipelineDefinition<TContext> to use [Pipeline]",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericPipelineRequiresContextAttribute = new(
        "PLNGEN003", "Generic pipeline requires explicit context attribute",
        "Pipeline '{0}' inherits PipelineDefinition<TContext> and must declare [Pipeline(typeof({1}))]",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor PipelineContextMismatch = new(
        "PLNGEN004", "Pipeline context mismatch",
        "Pipeline '{0}' declares context '{1}' in [Pipeline] but inherits '{2}'",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ManualConfigureNotAllowed = new(
        "PLNGEN005", "Generated pipelines must not implement Configure manually",
        "Pipeline '{0}' uses [Pipeline] and must not declare its own Configure(...) implementation",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor StepMethodMustNotBeGeneric = new(
        "PLNGEN006", "Pipeline step method must not be generic",
        "Pipeline step method '{0}' must not be generic",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor StepMethodSignatureNotSupported = new(
        "PLNGEN007", "Unsupported pipeline step method signature",
        "Pipeline step method '{0}' has an unsupported signature. Supported return types are void, Task, Result, Task<Result>, PipelineControl, and Task<PipelineControl>",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor AsyncVoidNotAllowed = new(
        "PLNGEN008", "async void pipeline steps are not supported",
        "Pipeline step method '{0}' must not return async void",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor DuplicateStepOrder = new(
        "PLNGEN009", "Duplicate pipeline step order",
        "Pipeline '{0}' contains duplicate generated step order '{1}'",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor DuplicateStepName = new(
        "PLNGEN010", "Duplicate pipeline step name",
        "Pipeline '{0}' contains duplicate generated step name '{1}'",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor InvalidHookType = new(
        "PLNGEN011", "Invalid pipeline hook type",
        "Type '{0}' is not a compatible pipeline hook for pipeline '{1}'",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor InvalidBehaviorType = new(
        "PLNGEN012", "Invalid pipeline behavior type",
        "Type '{0}' is not a compatible pipeline behavior for pipeline '{1}'",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ContextParameterNotAllowed = new(
        "PLNGEN013", "No-context pipeline step must not declare a context parameter",
        "Pipeline step method '{0}' belongs to a no-context pipeline and must not declare a pipeline context parameter",
        "Pipeline.CodeGen", DiagnosticSeverity.Error, true);
#pragma warning restore RS2008 // Enable analyzer release tracking
#pragma warning restore RS1032 // Define diagnostic message correctly
}

public class PipelineGenerationModel(
    INamedTypeSymbol classSymbol,
    bool isGenericPipeline,
    ITypeSymbol contextType,
    ImmutableArray<PipelineTypeRegistration> hookRegistrations,
    ImmutableArray<PipelineTypeRegistration> behaviorRegistrations,
    ImmutableArray<PipelineStepGenerationModel> steps)
{
    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;

    public bool IsGenericPipeline { get; } = isGenericPipeline;

    public ITypeSymbol ContextType { get; } = contextType;

    public ImmutableArray<PipelineTypeRegistration> HookRegistrations { get; } = hookRegistrations;

    public ImmutableArray<PipelineTypeRegistration> BehaviorRegistrations { get; } = behaviorRegistrations;

    public ImmutableArray<PipelineStepGenerationModel> Steps { get; } = steps;

    public static PipelineGenerationModel Create(SourceProductionContext context, Compilation compilation, INamedTypeSymbol classSymbol)
    {
        var pipelineAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == PipelineSourceGenerator.PipelineAttributeName);
        if (pipelineAttribute is null)
        {
            return null;
        }

        if (!IsPartial(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.PipelineMustBePartial,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        if (HasManualConfigure(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.ManualConfigureNotAllowed,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        if (!TryGetPipelineShape(compilation, classSymbol, out var isGenericPipeline, out var inheritedContextType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.PipelineMustInheritPipelineDefinition,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        var declaredContextType = pipelineAttribute.ConstructorArguments.Length == 1
            ? pipelineAttribute.ConstructorArguments[0].Value as ITypeSymbol
            : null;

        if (isGenericPipeline && declaredContextType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.GenericPipelineRequiresContextAttribute,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                inheritedContextType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return null;
        }

        if (!isGenericPipeline && declaredContextType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.PipelineContextMismatch,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                declaredContextType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                "no context"));
            return null;
        }

        if (declaredContextType is not null &&
            !SymbolEqualityComparer.Default.Equals(declaredContextType, inheritedContextType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.PipelineContextMismatch,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                declaredContextType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                inheritedContextType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return null;
        }

        var hooks = GetRegistrations(
            context,
            classSymbol,
            compilation,
            PipelineSourceGenerator.PipelineHookAttributeName,
            PipelineSourceGenerator.PipelineHookOfTName,
            PipelineSourceGeneratorDiagnostics.InvalidHookType,
            inheritedContextType);
        if (hooks.IsDefault)
        {
            return null;
        }

        var behaviors = GetRegistrations(
            context,
            classSymbol,
            compilation,
            PipelineSourceGenerator.PipelineBehaviorAttributeName,
            PipelineSourceGenerator.PipelineBehaviorOfTName,
            PipelineSourceGeneratorDiagnostics.InvalidBehaviorType,
            inheritedContextType);
        if (behaviors.IsDefault)
        {
            return null;
        }

        // Step methods are gathered from the attributed pipeline class and then normalized into generated registrations.
        var steps = new List<PipelineStepGenerationModel>();
        foreach (var method in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var stepAttribute = method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == PipelineSourceGenerator.PipelineStepAttributeName);
            if (stepAttribute is null)
            {
                continue;
            }

            var step = PipelineStepGenerationModel.Create(context, compilation, classSymbol, method, stepAttribute, inheritedContextType, isGenericPipeline);
            if (step is null)
            {
                return null;
            }

            steps.Add(step);
        }

        var duplicateOrder = steps.GroupBy(s => s.Order).FirstOrDefault(g => g.Count() > 1);
        if (duplicateOrder is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.DuplicateStepOrder,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                duplicateOrder.Key));
            return null;
        }

        var duplicateName = steps.GroupBy(s => s.StepName, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicateName is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.DuplicateStepName,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                duplicateName.Key));
            return null;
        }

        return new PipelineGenerationModel(
            classSymbol,
            isGenericPipeline,
            inheritedContextType,
            hooks,
            behaviors,
            steps.OrderBy(s => s.Order).ToImmutableArray());
    }

    private static bool TryGetPipelineShape(Compilation compilation, INamedTypeSymbol classSymbol, out bool isGenericPipeline, out ITypeSymbol contextType)
    {
        var genericPipelineType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.PipelineDefinitionOfTName);
        var nonGenericPipelineType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.PipelineDefinitionName);
        var nullContextType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.NullPipelineContextName);

        for (var current = classSymbol; current is not null; current = current.BaseType)
        {
            if (genericPipelineType is not null &&
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, genericPipelineType))
            {
                isGenericPipeline = true;
                contextType = current.TypeArguments[0];
                return true;
            }

            if (nonGenericPipelineType is not null &&
                SymbolEqualityComparer.Default.Equals(current, nonGenericPipelineType))
            {
                isGenericPipeline = false;
                contextType = nullContextType;
                return true;
            }
        }

        isGenericPipeline = false;
        contextType = null;
        return false;
    }

    private static bool IsPartial(INamedTypeSymbol classSymbol)
    {
        return classSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .All(c => c.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));
    }

    private static bool HasManualConfigure(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetMembers("Configure")
            .OfType<IMethodSymbol>()
            .Any(m => !m.IsImplicitlyDeclared);
    }

    private static ImmutableArray<PipelineTypeRegistration> GetRegistrations(
        SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        string attributeName,
        string componentInterfaceName,
        DiagnosticDescriptor descriptor,
        ITypeSymbol pipelineContextType)
    {
        var registrations = classSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == attributeName)
            .OrderBy(a => a.ApplicationSyntaxReference?.Span.Start ?? int.MaxValue)
            .Select(a => a.ConstructorArguments.Length == 1 ? a.ConstructorArguments[0].Value as INamedTypeSymbol : null)
            .Where(t => t is not null)
            .ToList();

        var componentInterface = compilation.GetTypeByMetadataName(componentInterfaceName);
        var nullContextType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.NullPipelineContextName);

        foreach (var type in registrations)
        {
            var componentContext = type.AllInterfaces
                .FirstOrDefault(i => componentInterface is not null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, componentInterface))
                ?.TypeArguments[0];

            if (componentContext is null || !IsCompatibleContext(componentContext, pipelineContextType, nullContextType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    classSymbol.Locations.FirstOrDefault(),
                    type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    classSymbol.Name));
                return default;
            }
        }

        return registrations.Select(t => new PipelineTypeRegistration(t)).ToImmutableArray();
    }

    private static bool IsCompatibleContext(ITypeSymbol componentContext, ITypeSymbol pipelineContext, ITypeSymbol nullContextType)
    {
        if (nullContextType is not null && SymbolEqualityComparer.Default.Equals(componentContext, nullContextType))
        {
            return true;
        }

        return IsAssignableFrom(componentContext, pipelineContext);
    }

    private static bool IsAssignableFrom(ITypeSymbol baseType, ITypeSymbol candidateType)
    {
        if (baseType is null || candidateType is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(baseType, candidateType))
        {
            return true;
        }

        for (var current = candidateType.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, current))
            {
                return true;
            }
        }

        return false;
    }
}

public class PipelineTypeRegistration(INamedTypeSymbol typeSymbol)
{
    public INamedTypeSymbol TypeSymbol { get; } = typeSymbol;
}

public enum PipelineStepReturnKind
{
    Void,
    Task,
    Result,
    TaskOfResult,
    PipelineControl,
    TaskOfPipelineControl
}

public enum PipelineStepParameterKind
{
    Context,
    Result,
    CancellationToken,
    Service
}

public class PipelineStepParameterModel(IParameterSymbol symbol, PipelineStepParameterKind kind)
{
    public IParameterSymbol Symbol { get; } = symbol;

    public PipelineStepParameterKind Kind { get; } = kind;
}

public class PipelineStepGenerationModel(
    IMethodSymbol methodSymbol,
    int order,
    string stepName,
    string generatedTypeName,
    bool isAsync,
    bool isStatic,
    PipelineStepReturnKind returnKind,
    ImmutableArray<PipelineStepParameterModel> parameters)
{
    public IMethodSymbol MethodSymbol { get; } = methodSymbol;

    public int Order { get; } = order;

    public string StepName { get; } = stepName;

    public string GeneratedTypeName { get; } = generatedTypeName;

    public bool IsAsync { get; } = isAsync;

    public bool IsStatic { get; } = isStatic;

    public PipelineStepReturnKind ReturnKind { get; } = returnKind;

    public ImmutableArray<PipelineStepParameterModel> Parameters { get; } = parameters;

    public static PipelineStepGenerationModel Create(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol classSymbol,
        IMethodSymbol method,
        AttributeData stepAttribute,
        ITypeSymbol pipelineContextType,
        bool isGenericPipeline)
    {
        if (method.IsGenericMethod)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.StepMethodMustNotBeGeneric,
                method.Locations.FirstOrDefault(),
                method.Name));
            return null;
        }

        if (method.ReturnsVoid && method.IsAsync)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.AsyncVoidNotAllowed,
                method.Locations.FirstOrDefault(),
                method.Name));
            return null;
        }

        if (!TryGetReturnKind(compilation, method.ReturnType, out var returnKind))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.StepMethodSignatureNotSupported,
                method.Locations.FirstOrDefault(),
                method.Name));
            return null;
        }

        var parameters = new List<PipelineStepParameterModel>();
        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsParams)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    PipelineSourceGeneratorDiagnostics.StepMethodSignatureNotSupported,
                    parameter.Locations.FirstOrDefault(),
                    method.Name));
                return null;
            }

            // Any parameter that is not one of the recognized runtime inputs is treated as a DI service dependency.
            PipelineStepParameterKind kind;
            if (isGenericPipeline && SymbolEqualityComparer.Default.Equals(parameter.Type, pipelineContextType))
            {
                kind = PipelineStepParameterKind.Context;
            }
            else if (!isGenericPipeline && InheritsFrom(parameter.Type, compilation.GetTypeByMetadataName(PipelineSourceGenerator.PipelineContextBaseName)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    PipelineSourceGeneratorDiagnostics.ContextParameterNotAllowed,
                    parameter.Locations.FirstOrDefault(),
                    method.Name));
                return null;
            }
            else if (IsType(parameter.Type, compilation.GetTypeByMetadataName(PipelineSourceGenerator.ResultName)))
            {
                kind = PipelineStepParameterKind.Result;
            }
            else if (IsType(parameter.Type, compilation.GetTypeByMetadataName(PipelineSourceGenerator.CancellationTokenName)))
            {
                kind = PipelineStepParameterKind.CancellationToken;
            }
            else
            {
                kind = PipelineStepParameterKind.Service;
            }

            parameters.Add(new PipelineStepParameterModel(parameter, kind));
        }

        var stepName = GetStepName(method, stepAttribute);
        var generatedTypeName = $"__GeneratedStep_{stepAttribute.ConstructorArguments[0].Value}_{SanitizeMethodName(method.Name)}";

        return new PipelineStepGenerationModel(
            method,
            (int)stepAttribute.ConstructorArguments[0].Value,
            stepName,
            generatedTypeName,
            returnKind is PipelineStepReturnKind.Task or PipelineStepReturnKind.TaskOfResult or PipelineStepReturnKind.TaskOfPipelineControl,
            method.IsStatic,
            returnKind,
            parameters.ToImmutableArray());
    }

    private static bool TryGetReturnKind(Compilation compilation, ITypeSymbol returnType, out PipelineStepReturnKind returnKind)
    {
        var resultType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.ResultName);
        var pipelineControlType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.PipelineControlName);
        var taskType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.TaskName);
        var taskOfType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.TaskOfTName);

        if (returnType.SpecialType == SpecialType.System_Void)
        {
            returnKind = PipelineStepReturnKind.Void;
            return true;
        }

        if (IsType(returnType, resultType))
        {
            returnKind = PipelineStepReturnKind.Result;
            return true;
        }

        if (IsType(returnType, pipelineControlType))
        {
            returnKind = PipelineStepReturnKind.PipelineControl;
            return true;
        }

        if (returnType is INamedTypeSymbol named && named.IsGenericType &&
            taskOfType is not null &&
            SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, taskOfType))
        {
            var inner = named.TypeArguments[0];
            if (IsType(inner, resultType))
            {
                returnKind = PipelineStepReturnKind.TaskOfResult;
                return true;
            }

            if (IsType(inner, pipelineControlType))
            {
                returnKind = PipelineStepReturnKind.TaskOfPipelineControl;
                return true;
            }
        }

        if (IsType(returnType, taskType))
        {
            returnKind = PipelineStepReturnKind.Task;
            return true;
        }

        returnKind = default;
        return false;
    }

    private static bool InheritsFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        if (type is null || baseType is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type, baseType))
        {
            return true;
        }

        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsType(ITypeSymbol type, ITypeSymbol expectedType)
    {
        return type is not null &&
            expectedType is not null &&
            SymbolEqualityComparer.Default.Equals(type, expectedType);
    }

    private static string GetStepName(IMethodSymbol method, AttributeData stepAttribute)
    {
        var explicitName = stepAttribute.NamedArguments
            .FirstOrDefault(a => a.Key == "Name")
            .Value.Value as string;

        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName;
        }

        var methodName = method.Name.EndsWith("Async", StringComparison.Ordinal)
            ? method.Name.Substring(0, method.Name.Length - 5)
            : method.Name;

        return ToKebabCase(methodName);
    }

    private static string SanitizeMethodName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString();
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current))
            {
                if (i > 0)
                {
                    var previous = value[i - 1];
                    var nextIsLower = i + 1 < value.Length && char.IsLower(value[i + 1]);
                    if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                    {
                        builder.Append('-');
                    }
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else if (current == '_' || current == ' ')
            {
                builder.Append('-');
            }
            else
            {
                builder.Append(char.ToLowerInvariant(current));
            }
        }

        return builder.ToString();
    }
}

public static class PipelineSourceEmitter
{
    public static string Emit(PipelineGenerationModel model)
    {
        var ns = model.ClassSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : model.ClassSymbol.ContainingNamespace.ToDisplayString();
        var className = model.ClassSymbol.Name;
        var builderType = model.IsGenericPipeline
            ? $"global::BridgingIT.DevKit.Common.IPipelineDefinitionBuilder<{ToTypeDisplay(model.ContextType)}>"
            : "global::BridgingIT.DevKit.Common.IPipelineDefinitionBuilder";

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("// source: PipelineSourceGenerator");

        if (!string.IsNullOrWhiteSpace(ns))
        {
            source.AppendLine($"namespace {ns}");
            source.AppendLine("{");
        }

        source.AppendLine($"partial class {className}");
        source.AppendLine("{");
        source.AppendLine($"    partial void OnConfigureGenerated({builderType} builder);");
        source.AppendLine();
        source.AppendLine($"    protected override void Configure({builderType} builder)");
        source.AppendLine("    {");

        foreach (var hook in model.HookRegistrations)
        {
            source.AppendLine($"        builder.AddHook<{ToTypeDisplay(hook.TypeSymbol)}>();");
        }

        foreach (var behavior in model.BehaviorRegistrations)
        {
            source.AppendLine($"        builder.AddBehavior<{ToTypeDisplay(behavior.TypeSymbol)}>();");
        }

        foreach (var step in model.Steps)
        {
            source.AppendLine($"        builder.AddStep<{step.GeneratedTypeName}>(\"{Escape(step.StepName)}\");");
        }

        // The optional partial extension keeps generated pipelines open for one-off manual additions.
        source.AppendLine("        OnConfigureGenerated(builder);");
        source.AppendLine("    }");
        source.AppendLine();

        foreach (var step in model.Steps)
        {
            EmitStep(source, model, step);
            source.AppendLine();
        }

        source.AppendLine("}");

        if (!string.IsNullOrWhiteSpace(ns))
        {
            source.AppendLine("}");
        }

        return source.ToString();
    }

    private static void EmitStep(StringBuilder source, PipelineGenerationModel model, PipelineStepGenerationModel step)
    {
        var baseType = step.IsAsync
            ? model.IsGenericPipeline
                ? $"global::BridgingIT.DevKit.Common.AsyncPipelineStep<{ToTypeDisplay(model.ContextType)}>"
                : "global::BridgingIT.DevKit.Common.AsyncPipelineStep"
            : model.IsGenericPipeline
                ? $"global::BridgingIT.DevKit.Common.PipelineStep<{ToTypeDisplay(model.ContextType)}>"
                : "global::BridgingIT.DevKit.Common.PipelineStep";

        source.AppendLine($"    public class {step.GeneratedTypeName} : {baseType}");
        source.AppendLine("    {");

        if (!step.IsStatic)
        {
            source.AppendLine($"        private readonly {ToTypeDisplay(model.ClassSymbol)} owner;");
        }

        foreach (var parameter in step.Parameters.Where(p => p.Kind == PipelineStepParameterKind.Service))
        {
            source.AppendLine($"        private readonly {ToTypeDisplay(parameter.Symbol.Type)} {GetFieldName(parameter.Symbol.Name)};");
        }

        var ctorParameters = new List<string>();
        if (!step.IsStatic)
        {
            ctorParameters.Add($"{ToTypeDisplay(model.ClassSymbol)} owner");
        }

        ctorParameters.AddRange(step.Parameters
            .Where(p => p.Kind == PipelineStepParameterKind.Service)
            .Select(p => $"{ToTypeDisplay(p.Symbol.Type)} {p.Symbol.Name}"));

        source.AppendLine($"        public {step.GeneratedTypeName}({string.Join(", ", ctorParameters)})");
        source.AppendLine("        {");
        if (!step.IsStatic)
        {
            source.AppendLine("            this.owner = owner;");
        }

        foreach (var parameter in step.Parameters.Where(p => p.Kind == PipelineStepParameterKind.Service))
        {
            source.AppendLine($"            this.{GetFieldName(parameter.Symbol.Name)} = {parameter.Symbol.Name};");
        }

        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine($"        public override string Name => \"{Escape(step.StepName)}\";");
        source.AppendLine();

        if (step.IsAsync)
        {
            if (model.IsGenericPipeline)
            {
                source.AppendLine($"        protected override async global::System.Threading.Tasks.ValueTask<global::BridgingIT.DevKit.Common.PipelineControl> ExecuteAsync({ToTypeDisplay(model.ContextType)} context, global::BridgingIT.DevKit.Common.Result result, global::BridgingIT.DevKit.Common.PipelineExecutionOptions options, global::System.Threading.CancellationToken cancellationToken)");
            }
            else
            {
                source.AppendLine("        protected override async global::System.Threading.Tasks.ValueTask<global::BridgingIT.DevKit.Common.PipelineControl> ExecuteAsync(global::BridgingIT.DevKit.Common.Result result, global::BridgingIT.DevKit.Common.PipelineExecutionOptions options, global::System.Threading.CancellationToken cancellationToken)");
            }
        }
        else if (model.IsGenericPipeline)
        {
            source.AppendLine($"        protected override global::BridgingIT.DevKit.Common.PipelineControl Execute({ToTypeDisplay(model.ContextType)} context, global::BridgingIT.DevKit.Common.Result result, global::BridgingIT.DevKit.Common.PipelineExecutionOptions options)");
        }
        else
        {
            source.AppendLine("        protected override global::BridgingIT.DevKit.Common.PipelineControl Execute(global::BridgingIT.DevKit.Common.Result result, global::BridgingIT.DevKit.Common.PipelineExecutionOptions options)");
        }

        source.AppendLine("        {");
        EmitInvocation(source, model, step);
        source.AppendLine("        }");
        source.AppendLine("    }");
    }

    private static void EmitInvocation(StringBuilder source, PipelineGenerationModel model, PipelineStepGenerationModel step)
    {
        var invocationTarget = step.IsStatic
            ? ToTypeDisplay(model.ClassSymbol)
            : "this.owner";

        var args = step.Parameters.Select(p => p.Kind switch
        {
            PipelineStepParameterKind.Context => "context",
            PipelineStepParameterKind.Result => "result",
            PipelineStepParameterKind.CancellationToken => "cancellationToken",
            _ => $"this.{GetFieldName(p.Symbol.Name)}"
        });

        var invocation = $"{invocationTarget}.{step.MethodSymbol.Name}({string.Join(", ", args)})";

        // Generated wrappers normalize the author-friendly method shapes back to PipelineControl.
        switch (step.ReturnKind)
        {
            case PipelineStepReturnKind.Void:
                source.AppendLine($"            {invocation};");
                source.AppendLine("            return global::BridgingIT.DevKit.Common.PipelineControl.Continue(result);");
                break;
            case PipelineStepReturnKind.Task:
                source.AppendLine($"            await {invocation};");
                source.AppendLine("            return global::BridgingIT.DevKit.Common.PipelineControl.Continue(result);");
                break;
            case PipelineStepReturnKind.Result:
                source.AppendLine($"            var nextResult = {invocation};");
                source.AppendLine("            return global::BridgingIT.DevKit.Common.PipelineControl.Continue(nextResult);");
                break;
            case PipelineStepReturnKind.TaskOfResult:
                source.AppendLine($"            var nextResult = await {invocation};");
                source.AppendLine("            return global::BridgingIT.DevKit.Common.PipelineControl.Continue(nextResult);");
                break;
            case PipelineStepReturnKind.PipelineControl:
                source.AppendLine($"            return {invocation};");
                break;
            case PipelineStepReturnKind.TaskOfPipelineControl:
                source.AppendLine($"            return await {invocation};");
                break;
        }
    }

    private static string ToTypeDisplay(ITypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string GetFieldName(string parameterName)
    {
        return "_" + char.ToLowerInvariant(parameterName[0]) + parameterName.Substring(1);
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
