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
/// Generates packaged pipeline definition plumbing and wrapper steps for attributed pipeline classes.
/// </summary>
[Generator]
public sealed class PipelineSourceGenerator : IIncrementalGenerator
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
                predicate: static (node, _) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0,
                transform: static (ctx, _) => PipelineGenerationModelBuilder.GetCandidate(ctx))
            .Where(static symbol => symbol is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(pipelineClasses.Collect());

        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) =>
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

                var model = PipelineGenerationModelBuilder.Create(spc, compilation, classSymbol);
                if (model is null)
                {
                    continue;
                }

                spc.AddSource(
                    $"{PipelineGeneratorSymbolHelper.SanitizeHintName(model.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}.Pipeline.g.cs",
                    PipelineSourceEmitter.Emit(model));
            }
        });
    }
}

#pragma warning restore RS1038
#pragma warning restore RS1042
#pragma warning restore RS1036
#pragma warning restore RS1035
