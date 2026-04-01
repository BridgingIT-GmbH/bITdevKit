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
/// Generates Notifier handlers, validator glue, and convenience helpers for attributed event types.
/// </summary>
[Generator]
public sealed class NotifierSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var eventTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0,
                transform: static (ctx, _) => NotifierGenerationModelBuilder.GetCandidate(ctx))
            .Where(static symbol => symbol is not null);

        var compilationAndTypes = context.CompilationProvider.Combine(eventTypes.Collect());

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

                var model = NotifierGenerationModelBuilder.Create(spc, compilation, classSymbol);
                if (model is null)
                {
                    continue;
                }

                spc.AddSource(
                    $"{RequesterGeneratorSymbolHelper.SanitizeHintName(model.EventTypeName)}.Notifier.g.cs",
                    NotifierSourceEmitter.Emit(model));
            }
        });
    }
}

#pragma warning restore RS1038
#pragma warning restore RS1042
#pragma warning restore RS1036
#pragma warning restore RS1035
