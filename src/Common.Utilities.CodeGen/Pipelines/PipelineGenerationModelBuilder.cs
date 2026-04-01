// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Builds validated semantic models for pipeline source generation.
/// </summary>
public static class PipelineGenerationModelBuilder
{
    /// <summary>
    /// Returns the attributed pipeline type symbol when the current syntax node is a pipeline code-generation candidate.
    /// </summary>
    /// <param name="context">The generator syntax context for the candidate node.</param>
    /// <returns>The attributed pipeline type symbol, or <see langword="null"/> when the node is not a pipeline candidate.</returns>
    public static INamedTypeSymbol GetCandidate(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        return classSymbol.GetAttributes().Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == PipelineSourceGenerator.PipelineAttributeName)
            ? classSymbol
            : null;
    }

    /// <summary>
    /// Creates a validated generation model for an attributed pipeline definition.
    /// </summary>
    /// <param name="context">The source-production context used to report diagnostics.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="classSymbol">The authored pipeline type.</param>
    /// <returns>A generation model when the pipeline is valid; otherwise <see langword="null"/>.</returns>
    public static PipelineGenerationModel Create(SourceProductionContext context, Compilation compilation, INamedTypeSymbol classSymbol)
    {
        var pipelineAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == PipelineSourceGenerator.PipelineAttributeName);
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

        if (HasExplicitBaseType(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.PipelineMustNotDeclareBaseType,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        var declaredContextType = pipelineAttribute.ConstructorArguments.Length == 1
            ? pipelineAttribute.ConstructorArguments[0].Value as ITypeSymbol
            : null;

        var pipelineContextBaseType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.PipelineContextBaseName);
        var nullContextType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.NullPipelineContextName);
        if (declaredContextType is not null && !IsAssignableFrom(pipelineContextBaseType, declaredContextType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.InvalidPipelineContextType,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                declaredContextType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return null;
        }

        var isGenericPipeline = declaredContextType is not null;
        var pipelineContextType = declaredContextType ?? nullContextType;

        var hooks = GetRegistrations(
            context,
            classSymbol,
            compilation,
            PipelineSourceGenerator.PipelineHookAttributeName,
            PipelineSourceGenerator.PipelineHookOfTName,
            PipelineSourceGeneratorDiagnostics.InvalidHookType,
            pipelineContextType);
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
            pipelineContextType);
        if (behaviors.IsDefault)
        {
            return null;
        }

        // Step methods are gathered from the authored pipeline class and normalized into
        // generated wrapper types that all expose the same runtime step contract.
        var steps = new List<PipelineStepGenerationModel>();
        foreach (var method in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var stepAttribute = method.GetAttributes()
                .FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == PipelineSourceGenerator.PipelineStepAttributeName);
            if (stepAttribute is null)
            {
                continue;
            }

            var step = PipelineStepGenerationModelBuilder.Create(
                context,
                compilation,
                method,
                stepAttribute,
                pipelineContextType,
                isGenericPipeline);
            if (step is null)
            {
                return null;
            }

            steps.Add(step);
        }

        var duplicateOrder = steps.GroupBy(static step => step.Order).FirstOrDefault(static group => group.Count() > 1);
        if (duplicateOrder is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineSourceGeneratorDiagnostics.DuplicateStepOrder,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                duplicateOrder.Key));
            return null;
        }

        var duplicateName = steps.GroupBy(static step => step.StepName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
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
            pipelineContextType,
            hooks,
            behaviors,
            steps.OrderBy(static step => step.Order).ToImmutableArray());
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
            .Where(attribute => attribute.AttributeClass?.ToDisplayString() == attributeName)
            .OrderBy(static attribute => attribute.ApplicationSyntaxReference?.Span.Start ?? int.MaxValue)
            .Select(static attribute => attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null)
            .Where(static type => type is not null)
            .ToList();

        var componentInterface = compilation.GetTypeByMetadataName(componentInterfaceName);
        var nullContextType = compilation.GetTypeByMetadataName(PipelineSourceGenerator.NullPipelineContextName);

        foreach (var type in registrations)
        {
            var componentContext = type.AllInterfaces
                .FirstOrDefault(@interface => componentInterface is not null &&
                    SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, componentInterface))
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

        return registrations.Select(static type => new PipelineTypeRegistration(type)).ToImmutableArray();
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

    private static bool IsPartial(INamedTypeSymbol classSymbol)
    {
        return classSymbol.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .All(static declaration => declaration.Modifiers.Any(modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));
    }

    private static bool HasExplicitBaseType(INamedTypeSymbol classSymbol)
    {
        return classSymbol.BaseType is { SpecialType: not SpecialType.System_Object };
    }

    private static bool HasManualConfigure(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetMembers("Configure")
            .OfType<IMethodSymbol>()
            .Any(static method => !method.IsImplicitlyDeclared);
    }
}
