// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#pragma warning disable RS1035
#pragma warning disable RS1036
#pragma warning disable RS1042
#pragma warning disable RS1038

/// <summary>
/// Generates nested FluentValidation validators for pipeline contexts using validation attributes or <see cref="ValidateAttribute"/>.
/// </summary>
[Generator]
public sealed class PipelineContextValidationSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var contextTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax classDeclaration &&
                    (classDeclaration.AttributeLists.Count > 0 ||
                        classDeclaration.Members.Any(static member =>
                            member is PropertyDeclarationSyntax property && property.AttributeLists.Count > 0 ||
                            member is MethodDeclarationSyntax method && method.AttributeLists.Count > 0)),
                transform: static (ctx, _) => PipelineContextValidationGenerationModelBuilder.GetCandidate(ctx))
            .Where(static symbol => symbol is not null);

        var compilationAndTypes = context.CompilationProvider.Combine(contextTypes.Collect());

        context.RegisterSourceOutput(compilationAndTypes, static (spc, source) =>
        {
            var (compilation, classSymbols) = source;
            var seen = new HashSet<string>(StringComparer.Ordinal);

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

                var model = PipelineContextValidationGenerationModelBuilder.Create(spc, compilation, classSymbol);
                if (model is null || !model.HasValidation)
                {
                    continue;
                }

                spc.AddSource(
                    $"{RequesterGeneratorSymbolHelper.SanitizeHintName(model.ContextTypeName)}.PipelineValidation.g.cs",
                    PipelineContextValidationSourceEmitter.Emit(model));
            }
        });
    }
}

#pragma warning restore RS1038
#pragma warning restore RS1042
#pragma warning restore RS1036
#pragma warning restore RS1035
