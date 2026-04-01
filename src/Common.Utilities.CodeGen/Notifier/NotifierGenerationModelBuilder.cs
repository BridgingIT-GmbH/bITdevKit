// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Builds validated semantic models for notifier source generation.
/// </summary>
public static class NotifierGenerationModelBuilder
{
    private const string EventAttributeName = "BridgingIT.DevKit.Common.EventAttribute";
    private const string HandleAttributeName = "BridgingIT.DevKit.Common.HandleAttribute";
    private const string ValidateAttributeName = "BridgingIT.DevKit.Common.ValidateAttribute";
    private const string NotificationBaseName = "BridgingIT.DevKit.Common.NotificationBase";
    private const string ResultName = "BridgingIT.DevKit.Common.Result";
    private const string PublishOptionsName = "BridgingIT.DevKit.Common.PublishOptions";
    private const string CancellationTokenName = "System.Threading.CancellationToken";
    private const string TaskOfTName = "System.Threading.Tasks.Task`1";
    private const string InlineValidatorOfTName = "FluentValidation.InlineValidator`1";

    private static readonly HashSet<string> SupportedPolicyAttributes =
    [
        "BridgingIT.DevKit.Common.HandlerAuthorizePolicyAttribute",
        "BridgingIT.DevKit.Common.HandlerAuthorizeRolesAttribute",
        "BridgingIT.DevKit.Common.HandlerRetryAttribute",
        "BridgingIT.DevKit.Common.HandlerTimeoutAttribute",
        "BridgingIT.DevKit.Common.HandlerChaosAttribute",
        "BridgingIT.DevKit.Common.HandlerCircuitBreakerAttribute",
        "BridgingIT.DevKit.Common.HandlerCacheInvalidateAttribute",
    ];

    /// <summary>
    /// Returns the attributed event type symbol when the current syntax node is a notifier code-generation candidate.
    /// </summary>
    /// <param name="context">The generator syntax context for the candidate node.</param>
    /// <returns>The attributed event type symbol, or <see langword="null"/> when the node is not a notifier candidate.</returns>
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

        return classSymbol.GetAttributes().Any(static attribute => attribute.AttributeClass?.ToDisplayString() == EventAttributeName)
            ? classSymbol
            : null;
    }

    /// <summary>
    /// Creates a validated generation model for a notifier event.
    /// </summary>
    /// <param name="context">The source-production context used to report diagnostics.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="classSymbol">The attributed event type.</param>
    /// <returns>A generation model when the event is valid; otherwise <see langword="null"/>.</returns>
    public static NotifierGenerationModel Create(SourceProductionContext context, Compilation compilation, INamedTypeSymbol classSymbol)
    {
        var eventAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == EventAttributeName);
        if (eventAttribute is null)
        {
            return null;
        }

        if (!IsValidEventType(context, classSymbol))
        {
            return null;
        }

        if (!TryResolveNotificationBaseStrategy(compilation, classSymbol, out var emitNotificationBase))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.InvalidEventBaseType,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        if (!TryGetHandleMethods(context, compilation, classSymbol, out var handleMethods))
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
                NotifierSourceGeneratorDiagnostics.ValidationGeneration,
                out var propertyValidationRules))
        {
            return null;
        }

        if (HasHelperCollisions(context, classSymbol))
        {
            return null;
        }

        if ((validateMethod is not null || propertyValidationRules.Length > 0) && HasTypeNameCollision(classSymbol, "Validator"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.GeneratedNameCollision,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                "Validator"));
            return null;
        }

        foreach (var handleMethod in handleMethods)
        {
            if (HasTypeNameCollision(classSymbol, handleMethod.GeneratedHandlerName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.GeneratedNameCollision,
                    handleMethod.MethodSymbol.Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                    classSymbol.Name,
                    handleMethod.GeneratedHandlerName));
                return null;
            }

            if (HasMethodNameCollision(classSymbol, handleMethod.BridgeMethodName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.GeneratedNameCollision,
                    handleMethod.MethodSymbol.Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                    classSymbol.Name,
                    handleMethod.BridgeMethodName));
                return null;
            }
        }

        return new NotifierGenerationModel(
            classSymbol,
            emitNotificationBase,
            handleMethods,
            validateMethod,
            propertyValidationRules,
            GetPolicyAttributes(classSymbol),
            classSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            RequesterGeneratorSymbolHelper.GetAccessibilityKeyword(classSymbol));
    }

    private static bool IsValidEventType(SourceProductionContext context, INamedTypeSymbol classSymbol)
    {
        if (classSymbol.ContainingType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.NestedEventsNotSupported,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (classSymbol.Arity > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.GenericEventsNotSupported,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (classSymbol.IsStatic || classSymbol.IsAbstract)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.InvalidAttributedType,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (!IsPartial(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.EventMustBePartial,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        return true;
    }

    private static bool TryResolveNotificationBaseStrategy(
        Compilation compilation,
        INamedTypeSymbol classSymbol,
        out bool emitNotificationBase)
    {
        var notificationBaseType = compilation.GetTypeByMetadataName(NotificationBaseName);
        if (notificationBaseType is null)
        {
            emitNotificationBase = false;
            return false;
        }

        for (var current = classSymbol.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, notificationBaseType))
            {
                emitNotificationBase = false;
                return true;
            }
        }

        var baseType = classSymbol.BaseType;
        if (baseType is null || baseType.SpecialType == SpecialType.System_Object)
        {
            emitNotificationBase = true;
            return true;
        }

        emitNotificationBase = false;
        return false;
    }

    private static bool TryGetHandleMethods(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol classSymbol,
        out ImmutableArray<NotifierHandleMethodModel> handleMethods)
    {
        var methods = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == HandleAttributeName))
            .OrderBy(static method => method.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
            .ToArray();

        if (methods.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.MissingHandleMethod,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            handleMethods = [];
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<NotifierHandleMethodModel>(methods.Length);
        var seenMethodNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in methods)
        {
            if (!seenMethodNames.Add(method.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.GeneratedNameCollision,
                    method.Locations.FirstOrDefault(),
                    classSymbol.Name,
                    method.Name));
                handleMethods = [];
                return false;
            }

            if (method.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.HandleMethodMustBeInstance,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                handleMethods = [];
                return false;
            }

            if (method.IsGenericMethod)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.HandleMethodMustNotBeGeneric,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                handleMethods = [];
                return false;
            }

            if (method.ReturnsVoid && method.IsAsync)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.AsyncVoidHandleNotAllowed,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                handleMethods = [];
                return false;
            }

            if (!TryGetHandleReturnShape(compilation, method.ReturnType, out var returnsTask))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.InvalidHandleReturnType,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                handleMethods = [];
                return false;
            }

            if (!TryGetHandleParameters(context, compilation, classSymbol, method, out var parameters))
            {
                handleMethods = [];
                return false;
            }

            builder.Add(new NotifierHandleMethodModel(
                method,
                returnsTask,
                parameters,
                "__NotifierGeneratedInvoke_" + method.Name + "Async",
                classSymbol.Name + "_" + method.Name + "GeneratedHandler"));
        }

        handleMethods = builder.ToImmutable();
        return true;
    }

    private static bool TryGetHandleParameters(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol classSymbol,
        IMethodSymbol method,
        out ImmutableArray<NotifierParameterModel> parameters)
    {
        var publishOptionsType = compilation.GetTypeByMetadataName(PublishOptionsName);
        var cancellationTokenType = compilation.GetTypeByMetadataName(CancellationTokenName);
        var builder = ImmutableArray.CreateBuilder<NotifierParameterModel>();
        var publishOptionsCount = 0;
        var cancellationCount = 0;

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsParams)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.InvalidHandleParameters,
                    parameter.Locations.FirstOrDefault(),
                    method.Name));
                parameters = [];
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, classSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotifierSourceGeneratorDiagnostics.InvalidHandleParameters,
                    parameter.Locations.FirstOrDefault(),
                    method.Name));
                parameters = [];
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, publishOptionsType))
            {
                publishOptionsCount++;
                builder.Add(new NotifierParameterModel(parameter, NotifierParameterKind.PublishOptions));
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
            {
                cancellationCount++;
                builder.Add(new NotifierParameterModel(parameter, NotifierParameterKind.CancellationToken));
                continue;
            }

            builder.Add(new NotifierParameterModel(parameter, NotifierParameterKind.Service));
        }

        if (publishOptionsCount > 1 || cancellationCount > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.InvalidHandleParameters,
                method.Locations.FirstOrDefault(),
                method.Name));
            parameters = [];
            return false;
        }

        parameters = builder.ToImmutable();
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
                NotifierSourceGeneratorDiagnostics.DuplicateValidateMethod,
                validateMethods[1].Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        var method = validateMethods[0];
        if (!method.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.ValidateMethodMustBeStatic,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (!method.ReturnsVoid)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.InvalidValidateReturnType,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (method.Parameters.Length != 1 || method.Parameters[0].RefKind != RefKind.None)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.InvalidValidateParameter,
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
                NotifierSourceGeneratorDiagnostics.InvalidValidateParameter,
                method.Locations.FirstOrDefault(),
                method.Name,
                classSymbol.Name));
            return false;
        }

        validateMethod = method;
        return true;
    }

    private static bool HasHelperCollisions(SourceProductionContext context, INamedTypeSymbol classSymbol)
    {
        var collisions = new List<(string Name, int ParameterCount)>
        {
            ("Success", 0),
            ("Failure", 0),
            ("Failure", 1),
        };

        foreach (var method in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (!collisions.Any(collision => collision.Name == method.Name && collision.ParameterCount == method.Parameters.Length))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                NotifierSourceGeneratorDiagnostics.GeneratedNameCollision,
                method.Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                method.Name));
            return true;
        }

        return false;
    }

    private static bool TryGetHandleReturnShape(Compilation compilation, ITypeSymbol returnType, out bool returnsTask)
    {
        var resultType = compilation.GetTypeByMetadataName(ResultName);
        var taskType = compilation.GetTypeByMetadataName(TaskOfTName);

        returnsTask = false;
        if (SymbolEqualityComparer.Default.Equals(returnType, resultType))
        {
            return true;
        }

        if (returnType is INamedTypeSymbol taskReturnType &&
            taskReturnType.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(taskReturnType.OriginalDefinition, taskType) &&
            SymbolEqualityComparer.Default.Equals(taskReturnType.TypeArguments[0], resultType))
        {
            returnsTask = true;
            return true;
        }

        return false;
    }

    private static ImmutableArray<AttributeData> GetPolicyAttributes(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetAttributes()
            .Where(static attribute => IsSupportedPolicyAttribute(attribute.AttributeClass))
            .OrderBy(static attribute => attribute.ApplicationSyntaxReference?.Span.Start ?? int.MaxValue)
            .ToImmutableArray();
    }

    private static bool IsSupportedPolicyAttribute(INamedTypeSymbol attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        var metadataName = attributeType.IsGenericType
            ? attributeType.ConstructedFrom.ToDisplayString()
            : attributeType.ToDisplayString();

        return SupportedPolicyAttributes.Contains(metadataName);
    }

    private static bool HasTypeNameCollision(INamedTypeSymbol classSymbol, string typeName)
    {
        return classSymbol.GetTypeMembers(typeName).Length > 0 ||
            classSymbol.ContainingNamespace.GetTypeMembers(typeName).Any(type => type.Name == typeName);
    }

    private static bool HasMethodNameCollision(INamedTypeSymbol classSymbol, string methodName)
    {
        return classSymbol.GetMembers(methodName).OfType<IMethodSymbol>().Any();
    }

    private static bool IsPartial(INamedTypeSymbol classSymbol)
    {
        return classSymbol.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .All(static declaration => declaration.Modifiers.Any(modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));
    }
}
