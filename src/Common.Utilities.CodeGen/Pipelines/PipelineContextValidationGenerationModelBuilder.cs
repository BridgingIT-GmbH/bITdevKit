// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Builds validated semantic models for pipeline-context validation source generation.
/// </summary>
public static class PipelineContextValidationGenerationModelBuilder
{
    private const string PipelineContextBaseName = "BridgingIT.DevKit.Common.PipelineContextBase";
    private const string ValidateAttributeName = "BridgingIT.DevKit.Common.ValidateAttribute";
    private const string InlineValidatorOfTName = "FluentValidation.InlineValidator`1";

    /// <summary>
    /// Returns the context type symbol when the current syntax node is a pipeline-context validation candidate.
    /// </summary>
    /// <param name="context">The generator syntax context for the candidate node.</param>
    /// <returns>The pipeline-context type symbol, or <see langword="null"/> when the node is not a candidate.</returns>
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

        return InheritsFrom(classSymbol, context.SemanticModel.Compilation.GetTypeByMetadataName(PipelineContextBaseName))
            ? classSymbol
            : null;
    }

    /// <summary>
    /// Creates a validated generation model for a pipeline context with source-generated validation metadata.
    /// </summary>
    /// <param name="context">The source-production context used to report diagnostics.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="classSymbol">The pipeline-context type candidate.</param>
    /// <returns>A generation model when the context is valid; otherwise <see langword="null"/>.</returns>
    public static PipelineContextValidationGenerationModel Create(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol classSymbol)
    {
        if (!IsValidContextType(context, compilation, classSymbol))
        {
            return null;
        }

        if (!TryGetValidateMethod(context, compilation, classSymbol, out var validateMethod))
        {
            return null;
        }

        if (!ValidationGenerationModelBuilder.TryCreate(
                context,
                classSymbol,
                PipelineContextValidationSourceGeneratorDiagnostics.ValidationGeneration,
                out var propertyValidationRules))
        {
            return null;
        }

        if (validateMethod is null && propertyValidationRules.Length == 0)
        {
            return null;
        }

        if (HasTypeNameCollision(classSymbol, "Validator"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.GeneratedNameCollision,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                "Validator"));
            return null;
        }

        return new PipelineContextValidationGenerationModel(
            classSymbol,
            validateMethod,
            propertyValidationRules,
            classSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : classSymbol.ContainingNamespace.ToDisplayString(),
            RequesterGeneratorSymbolHelper.GetAccessibilityKeyword(classSymbol));
    }

    private static bool IsValidContextType(SourceProductionContext context, Compilation compilation, INamedTypeSymbol classSymbol)
    {
        if (classSymbol.ContainingType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.NestedContextsNotSupported,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (classSymbol.Arity > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.GenericContextsNotSupported,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (classSymbol.IsStatic || classSymbol.IsAbstract ||
            !InheritsFrom(classSymbol, compilation.GetTypeByMetadataName(PipelineContextBaseName)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.InvalidAttributedType,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (!IsPartial(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.ContextMustBePartial,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        return true;
    }

    private static bool TryGetValidateMethod(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol classSymbol,
        out IMethodSymbol validateMethod)
    {
        var validateMethods = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == ValidateAttributeName))
            .ToArray();

        validateMethod = null;
        if (validateMethods.Length == 0)
        {
            return true;
        }

        if (validateMethods.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.DuplicateValidateMethod,
                validateMethods[1].Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        var method = validateMethods[0];
        if (!method.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.ValidateMethodMustBeStatic,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (!method.ReturnsVoid)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.InvalidValidateReturnType,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (method.Parameters.Length != 1 || method.Parameters[0].RefKind != RefKind.None)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.InvalidValidateParameter,
                method.Locations.FirstOrDefault(),
                method.Name,
                classSymbol.Name));
            return false;
        }

        var inlineValidatorType = compilation.GetTypeByMetadataName(InlineValidatorOfTName);
        var parameterType = method.Parameters[0].Type as INamedTypeSymbol;
        if (parameterType is null ||
            !parameterType.IsGenericType ||
            !SymbolEqualityComparer.Default.Equals(parameterType.OriginalDefinition, inlineValidatorType) ||
            !SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PipelineContextValidationSourceGeneratorDiagnostics.InvalidValidateParameter,
                method.Locations.FirstOrDefault(),
                method.Name,
                classSymbol.Name));
            return false;
        }

        validateMethod = method;
        return true;
    }

    private static bool HasTypeNameCollision(INamedTypeSymbol classSymbol, string typeName)
    {
        return classSymbol.GetTypeMembers(typeName).Length > 0;
    }

    private static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
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
}
