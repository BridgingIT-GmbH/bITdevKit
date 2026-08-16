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
    /// <summary>Gets the metadata name of the pipeline attribute.</summary>
    public const string PipelineAttributeName = "BridgingIT.DevKit.Common.PipelineAttribute";
    /// <summary>Gets the metadata name of the pipeline-step attribute.</summary>
    public const string PipelineStepAttributeName = "BridgingIT.DevKit.Common.PipelineStepAttribute";
    /// <summary>Gets the metadata name of the pipeline-hook attribute.</summary>
    public const string PipelineHookAttributeName = "BridgingIT.DevKit.Common.PipelineHookAttribute";
    /// <summary>Gets the metadata name of the pipeline-behavior attribute.</summary>
    public const string PipelineBehaviorAttributeName = "BridgingIT.DevKit.Common.PipelineBehaviorAttribute";
    /// <summary>Gets the metadata name of the non-generic pipeline definition.</summary>
    public const string PipelineDefinitionName = "BridgingIT.DevKit.Common.PipelineDefinition";
    /// <summary>Gets the metadata name of the generic pipeline definition.</summary>
    public const string PipelineDefinitionOfTName = "BridgingIT.DevKit.Common.PipelineDefinition`1";
    /// <summary>Gets the metadata name of the pipeline context base type.</summary>
    public const string PipelineContextBaseName = "BridgingIT.DevKit.Common.PipelineContextBase";
    /// <summary>Gets the metadata name of the null pipeline context.</summary>
    public const string NullPipelineContextName = "BridgingIT.DevKit.Common.NullPipelineContext";
    /// <summary>Gets the metadata name of the non-generic result type.</summary>
    public const string ResultName = "BridgingIT.DevKit.Common.Result";
    /// <summary>Gets the metadata name of the pipeline-control type.</summary>
    public const string PipelineControlName = "BridgingIT.DevKit.Common.PipelineControl";
    /// <summary>Gets the metadata name of the generic pipeline-hook interface.</summary>
    public const string PipelineHookOfTName = "BridgingIT.DevKit.Common.IPipelineHook`1";
    /// <summary>Gets the metadata name of the generic pipeline-behavior interface.</summary>
    public const string PipelineBehaviorOfTName = "BridgingIT.DevKit.Common.IPipelineBehavior`1";
    /// <summary>Gets the metadata name of <see cref="CancellationToken"/>.</summary>
    public const string CancellationTokenName = "System.Threading.CancellationToken";
    /// <summary>Gets the metadata name of the non-generic task type.</summary>
    public const string TaskName = "System.Threading.Tasks.Task";
    /// <summary>Gets the metadata name of the generic task type.</summary>
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
