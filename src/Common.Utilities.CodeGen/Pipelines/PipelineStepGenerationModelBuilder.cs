// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

/// <summary>
/// Builds validated step models for pipeline source generation.
/// </summary>
public static class PipelineStepGenerationModelBuilder
{
    /// <summary>
    /// Creates a validated step model for a developer-authored pipeline step method.
    /// </summary>
    /// <param name="context">The source-production context used to report diagnostics.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="method">The authored step method.</param>
    /// <param name="stepAttribute">The associated <c>[PipelineStep]</c> attribute.</param>
    /// <param name="pipelineContextType">The resolved pipeline context type.</param>
    /// <param name="isGenericPipeline">Indicates whether the pipeline uses a typed context.</param>
    /// <returns>A step model when the authored method is valid; otherwise <see langword="null"/>.</returns>
    public static PipelineStepGenerationModel Create(
        SourceProductionContext context,
        Compilation compilation,
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

            // The builder keeps the authored parameter model intentionally small:
            // known runtime inputs are classified explicitly and everything else is treated as a DI service.
            PipelineStepParameterKind kind;
            if (isGenericPipeline && SymbolEqualityComparer.Default.Equals(parameter.Type, pipelineContextType))
            {
                kind = PipelineStepParameterKind.Context;
            }
            else if (!isGenericPipeline &&
                InheritsFrom(parameter.Type, compilation.GetTypeByMetadataName(PipelineSourceGenerator.PipelineContextBaseName)))
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
        var generatedTypeName = $"__GeneratedStep_{stepAttribute.ConstructorArguments[0].Value}_{PipelineGeneratorSymbolHelper.SanitizeHintName(method.Name)}";

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

        if (returnType is INamedTypeSymbol namedReturnType &&
            namedReturnType.IsGenericType &&
            taskOfType is not null &&
            SymbolEqualityComparer.Default.Equals(namedReturnType.OriginalDefinition, taskOfType))
        {
            var innerType = namedReturnType.TypeArguments[0];
            if (IsType(innerType, resultType))
            {
                returnKind = PipelineStepReturnKind.TaskOfResult;
                return true;
            }

            if (IsType(innerType, pipelineControlType))
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

    private static string GetStepName(IMethodSymbol method, AttributeData stepAttribute)
    {
        var explicitName = stepAttribute.NamedArguments
            .FirstOrDefault(static argument => argument.Key == "Name")
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
            else if (current is '_' or ' ')
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
}
