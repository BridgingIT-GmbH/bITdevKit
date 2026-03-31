// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Builds validated semantic models for Requester source generation.
/// </summary>
public static class RequesterGenerationModelBuilder
{
    private const string CommandAttributeName = "BridgingIT.DevKit.Common.CommandAttribute";
    private const string QueryAttributeName = "BridgingIT.DevKit.Common.QueryAttribute";
    private const string HandleAttributeName = "BridgingIT.DevKit.Common.HandleAttribute";
    private const string ValidateAttributeName = "BridgingIT.DevKit.Common.ValidateAttribute";
    private const string RequestBaseOfTName = "BridgingIT.DevKit.Common.RequestBase`1";
    private const string ResultOfTName = "BridgingIT.DevKit.Common.Result`1";
    private const string UnitName = "BridgingIT.DevKit.Common.Unit";
    private const string SendOptionsName = "BridgingIT.DevKit.Common.SendOptions";
    private const string CancellationTokenName = "System.Threading.CancellationToken";
    private const string TaskOfTName = "System.Threading.Tasks.Task`1";
    private const string InlineValidatorOfTName = "FluentValidation.InlineValidator`1";

    /// <summary>
    /// Returns the attributed request type symbol when the current syntax node is a Requester code-generation candidate.
    /// </summary>
    /// <param name="context">The generator syntax context for the candidate node.</param>
    /// <returns>The attributed request type symbol, or <see langword="null"/> when the node is not a Requester candidate.</returns>
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
                attribute.AttributeClass?.ToDisplayString() is CommandAttributeName or QueryAttributeName)
            ? classSymbol
            : null;
    }

    /// <summary>
    /// Creates a validated generation model for a Requester command or query.
    /// </summary>
    /// <param name="context">The source-production context used to report diagnostics.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="classSymbol">The attributed request type.</param>
    /// <returns>A generation model when the request is valid; otherwise <see langword="null"/>.</returns>
    public static RequesterGenerationModel Create(SourceProductionContext context, Compilation compilation, INamedTypeSymbol classSymbol)
    {
        var commandAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == CommandAttributeName);
        var queryAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == QueryAttributeName);

        if (commandAttribute is null && queryAttribute is null)
        {
            return null;
        }

        if (!IsValidRequestType(context, classSymbol))
        {
            return null;
        }

        if (commandAttribute is not null && queryAttribute is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.CommandAndQueryConflict,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        // The handle method is validated first because its Result<T> return determines
        // the inferred response type when [Command] or [Query] omit an explicit typeof(...).
        var handleMethod = GetHandleMethod(context, compilation, classSymbol);
        if (handleMethod is null)
        {
            return null;
        }

        var unitType = compilation.GetTypeByMetadataName(UnitName);
        var responseType = ResolveResponseType(context, classSymbol, commandAttribute, queryAttribute, handleMethod, unitType);
        if (responseType is null)
        {
            return null;
        }

        // Partial requests can omit RequestBase<TResponse>; the generator only needs to
        // reject authored base types that would conflict with the resolved response shape.
        if (!TryResolveRequestBaseStrategy(compilation, classSymbol, responseType, out var emitRequestBase))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.RequestBaseMismatch,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                RequesterGeneratorSymbolHelper.GetTypeName(responseType)));
            return null;
        }

        if (!TryGetValidateMethod(context, compilation, classSymbol, out var validateMethod))
        {
            return null;
        }

        var generatedHandlerName = classSymbol.Name + "GeneratedHandler";
        if (HasTypeNameCollision(classSymbol, generatedHandlerName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.GeneratedNameCollision,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                generatedHandlerName));
            return null;
        }

        if (HasMethodNameCollision(classSymbol, RequesterGenerationModel.HandleBridgeMethodName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.GeneratedNameCollision,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                RequesterGenerationModel.HandleBridgeMethodName));
            return null;
        }

        if (HasHelperCollisions(context, classSymbol, responseType, unitType))
        {
            return null;
        }

        if (validateMethod is not null && HasTypeNameCollision(classSymbol, "Validator"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.GeneratedNameCollision,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                "Validator"));
            return null;
        }

        return new RequesterGenerationModel(
            classSymbol,
            queryAttribute is null ? RequesterRequestKind.Command : RequesterRequestKind.Query,
            responseType,
            emitRequestBase,
            handleMethod,
            validateMethod,
            RequesterGeneratorSymbolHelper.GetPolicyAttributes(classSymbol),
            classSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            generatedHandlerName,
            RequesterGeneratorSymbolHelper.GetAccessibilityKeyword(classSymbol));
    }

    private static bool IsValidRequestType(SourceProductionContext context, INamedTypeSymbol classSymbol)
    {
        if (classSymbol.ContainingType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.NestedRequestsNotSupported,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (classSymbol.Arity > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.GenericRequestsNotSupported,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (classSymbol.IsStatic || classSymbol.IsAbstract)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.InvalidAttributedType,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        if (!IsPartial(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.RequestMustBePartial,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        return true;
    }

    private static ITypeSymbol ResolveResponseType(
        SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        AttributeData commandAttribute,
        AttributeData queryAttribute,
        RequesterHandleMethodModel handleMethod,
        ITypeSymbol unitType)
    {
        var inferredResponseType = handleMethod.ResponseType;
        var explicitResponseType = (queryAttribute ?? commandAttribute)?.ConstructorArguments.Length == 1
            ? (queryAttribute ?? commandAttribute).ConstructorArguments[0].Value as ITypeSymbol
            : null;

        if ((queryAttribute ?? commandAttribute)?.ConstructorArguments.Length == 1 && explicitResponseType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.InvalidResponseType,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        if (queryAttribute is not null)
        {
            var responseType = explicitResponseType ?? inferredResponseType;

            if (SymbolEqualityComparer.Default.Equals(responseType, unitType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequesterSourceGeneratorDiagnostics.QueryUnitNotAllowed,
                    classSymbol.Locations.FirstOrDefault(),
                    classSymbol.Name));
                return null;
            }

            // Queries may use the short [Query] form, but if an explicit response type is present
            // it must still agree with the Result<T> shape returned by the authored handle method.
            if (explicitResponseType is not null &&
                !SymbolEqualityComparer.Default.Equals(explicitResponseType, inferredResponseType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequesterSourceGeneratorDiagnostics.ExplicitResponseTypeMismatch,
                    classSymbol.Locations.FirstOrDefault(),
                    classSymbol.Name,
                    RequesterGeneratorSymbolHelper.GetTypeName(explicitResponseType),
                    handleMethod.MethodSymbol.Name,
                    RequesterGeneratorSymbolHelper.GetTypeName(inferredResponseType)));
                return null;
            }

            return responseType;
        }

        if (explicitResponseType is not null &&
            !SymbolEqualityComparer.Default.Equals(explicitResponseType, inferredResponseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.ExplicitResponseTypeMismatch,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                RequesterGeneratorSymbolHelper.GetTypeName(explicitResponseType),
                handleMethod.MethodSymbol.Name,
                RequesterGeneratorSymbolHelper.GetTypeName(inferredResponseType)));
            return null;
        }

        return explicitResponseType ?? inferredResponseType;
    }

    private static bool TryResolveRequestBaseStrategy(
        Compilation compilation,
        INamedTypeSymbol classSymbol,
        ITypeSymbol responseType,
        out bool emitRequestBase)
    {
        var requestBaseType = compilation.GetTypeByMetadataName(RequestBaseOfTName);
        if (requestBaseType is null)
        {
            emitRequestBase = false;
            return false;
        }

        for (var current = classSymbol.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, requestBaseType) &&
                SymbolEqualityComparer.Default.Equals(current.TypeArguments[0], responseType))
            {
                emitRequestBase = false;
                return true;
            }
        }

        var baseType = classSymbol.BaseType;
        if (baseType is null || baseType.SpecialType == SpecialType.System_Object)
        {
            emitRequestBase = true;
            return true;
        }

        emitRequestBase = false;
        return false;
    }

    private static RequesterHandleMethodModel GetHandleMethod(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol classSymbol)
    {
        var handleMethods = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == HandleAttributeName))
            .ToArray();

        if (handleMethods.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.MissingHandleMethod,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        if (handleMethods.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.DuplicateHandleMethod,
                handleMethods[1].Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return null;
        }

        var method = handleMethods[0];
        if (method.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.HandleMethodMustBeInstance,
                method.Locations.FirstOrDefault(),
                method.Name));
            return null;
        }

        if (method.IsGenericMethod)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.HandleMethodMustNotBeGeneric,
                method.Locations.FirstOrDefault(),
                method.Name));
            return null;
        }

        if (method.ReturnsVoid && method.IsAsync)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.AsyncVoidHandleNotAllowed,
                method.Locations.FirstOrDefault(),
                method.Name));
            return null;
        }

        if (!TryGetHandleReturnShape(compilation, method.ReturnType, out var handleResponseType, out var returnsTask))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.InvalidHandleReturnType,
                method.Locations.FirstOrDefault(),
                method.Name,
                "TResponse"));
            return null;
        }

        var sendOptionsType = compilation.GetTypeByMetadataName(SendOptionsName);
        var cancellationTokenType = compilation.GetTypeByMetadataName(CancellationTokenName);
        var parameters = new List<RequesterParameterModel>();
        var sendOptionsCount = 0;
        var cancellationCount = 0;

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsParams)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequesterSourceGeneratorDiagnostics.InvalidHandleParameters,
                    parameter.Locations.FirstOrDefault(),
                    method.Name));
                return null;
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, classSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequesterSourceGeneratorDiagnostics.InvalidHandleParameters,
                    parameter.Locations.FirstOrDefault(),
                    method.Name));
                return null;
            }

            // Only a very small set of runtime-supplied parameters are recognized here.
            // Everything else is treated as a DI dependency resolved by the generated handler.
            if (SymbolEqualityComparer.Default.Equals(parameter.Type, sendOptionsType))
            {
                sendOptionsCount++;
                parameters.Add(new RequesterParameterModel(parameter, RequesterParameterKind.SendOptions));
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
            {
                cancellationCount++;
                parameters.Add(new RequesterParameterModel(parameter, RequesterParameterKind.CancellationToken));
                continue;
            }

            parameters.Add(new RequesterParameterModel(parameter, RequesterParameterKind.Service));
        }

        if (sendOptionsCount > 1 || cancellationCount > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.InvalidHandleParameters,
                method.Locations.FirstOrDefault(),
                method.Name));
            return null;
        }

        return new RequesterHandleMethodModel(method, handleResponseType, returnsTask, parameters.ToImmutableArray());
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
                RequesterSourceGeneratorDiagnostics.DuplicateValidateMethod,
                validateMethods[1].Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return false;
        }

        var method = validateMethods[0];
        if (!method.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.ValidateMethodMustBeStatic,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (!method.ReturnsVoid)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.InvalidValidateReturnType,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (method.Parameters.Length != 1 || method.Parameters[0].RefKind != RefKind.None)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.InvalidValidateParameter,
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
                RequesterSourceGeneratorDiagnostics.InvalidValidateParameter,
                method.Locations.FirstOrDefault(),
                method.Name,
                classSymbol.Name));
            return false;
        }

        validateMethod = method;
        return true;
    }

    private static bool HasHelperCollisions(
        SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        ITypeSymbol responseType,
        ITypeSymbol unitType)
    {
        var collisions = new List<(string Name, int ParameterCount)>();
        if (SymbolEqualityComparer.Default.Equals(responseType, unitType))
        {
            collisions.Add(("Success", 0));
            collisions.Add(("Failure", 0));
            collisions.Add(("Failure", 1));
        }
        else
        {
            collisions.Add(("Success", 1));
            collisions.Add(("Failure", 0));
            collisions.Add(("Failure", 1));
        }

        foreach (var method in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (!collisions.Any(collision => collision.Name == method.Name && collision.ParameterCount == method.Parameters.Length))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                RequesterSourceGeneratorDiagnostics.GeneratedNameCollision,
                method.Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                method.Name));
            return true;
        }

        return false;
    }

    private static bool TryGetHandleReturnShape(Compilation compilation, ITypeSymbol returnType, out ITypeSymbol responseType, out bool returnsTask)
    {
        var resultType = compilation.GetTypeByMetadataName(ResultOfTName);
        var taskType = compilation.GetTypeByMetadataName(TaskOfTName);

        responseType = null;
        returnsTask = false;
        if (returnType is INamedTypeSymbol namedReturnType &&
            namedReturnType.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(namedReturnType.OriginalDefinition, resultType))
        {
            responseType = namedReturnType.TypeArguments[0];
            return true;
        }

        if (returnType is INamedTypeSymbol taskReturnType &&
            taskReturnType.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(taskReturnType.OriginalDefinition, taskType) &&
            taskReturnType.TypeArguments[0] is INamedTypeSymbol innerResultType &&
            innerResultType.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(innerResultType.OriginalDefinition, resultType))
        {
            responseType = innerResultType.TypeArguments[0];
            returnsTask = true;
            return true;
        }

        return false;
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
