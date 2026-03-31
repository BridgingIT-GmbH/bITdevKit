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
/// Generates Requester handlers, validator glue, and convenience helpers for attributed request types.
/// </summary>
[Generator]
public sealed class RequesterSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var requestTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0,
                transform: static (ctx, _) => RequesterGenerationModelBuilder.GetCandidate(ctx))
            .Where(static symbol => symbol is not null);

        var compilationAndTypes = context.CompilationProvider.Combine(requestTypes.Collect());

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

                var model = RequesterGenerationModelBuilder.Create(spc, compilation, classSymbol);
                if (model is null)
                {
                    continue;
                }

                spc.AddSource(
                    $"{RequesterGeneratorSymbolHelper.SanitizeHintName(model.RequestTypeName)}.Requester.g.cs",
                    RequesterSourceEmitter.Emit(model));
            }
        });
    }
}

#pragma warning restore RS1038
#pragma warning restore RS1042
#pragma warning restore RS1036
#pragma warning restore RS1035
