// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;
/// <summary>
/// Provides dependency injection helpers for reusable diagram rendering.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the built-in Mermaid and SVG diagram renderers together with bitmap placeholder registrations and the shared <see cref="IDiagramRendererFactory"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRendering();
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var text = factory.Render(new StateDiagramBuilder().AddState("Created").Build()).GetText();
    /// </code>
    /// </example>
    public static IServiceCollection AddDiagramRendering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .TryAddDiagramRenderer<MermaidStateDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Mermaid)
            .TryAddDiagramRenderer<SvgStateDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Svg)
            .TryAddDiagramRenderer<BitmapDiagramRenderer>(DiagramKind.State, DiagramRenderFormat.Bitmap)
            .TryAddDiagramRenderer<MermaidFlowDiagramRenderer>(DiagramKind.Flow, DiagramRenderFormat.Mermaid)
            .TryAddDiagramRenderer<SvgFlowDiagramRenderer>(DiagramKind.Flow, DiagramRenderFormat.Svg)
            .TryAddDiagramRenderer<BitmapDiagramRenderer>(DiagramKind.Flow, DiagramRenderFormat.Bitmap)
            .TryAddDiagramRenderer<MermaidActivityDiagramRenderer>(DiagramKind.Activity, DiagramRenderFormat.Mermaid)
            .TryAddDiagramRenderer<SvgActivityDiagramRenderer>(DiagramKind.Activity, DiagramRenderFormat.Svg)
            .TryAddDiagramRenderer<BitmapDiagramRenderer>(DiagramKind.Activity, DiagramRenderFormat.Bitmap)
            .TryAddDiagramRenderer<MermaidSequenceDiagramRenderer>(DiagramKind.Sequence, DiagramRenderFormat.Mermaid)
            .TryAddDiagramRenderer<SvgSequenceDiagramRenderer>(DiagramKind.Sequence, DiagramRenderFormat.Svg)
            .TryAddDiagramRenderer<BitmapDiagramRenderer>(DiagramKind.Sequence, DiagramRenderFormat.Bitmap)
            .TryAddDiagramRenderer<MermaidClassDiagramRenderer>(DiagramKind.Class, DiagramRenderFormat.Mermaid)
            .TryAddDiagramRenderer<SvgClassDiagramRenderer>(DiagramKind.Class, DiagramRenderFormat.Svg)
            .TryAddDiagramRenderer<BitmapDiagramRenderer>(DiagramKind.Class, DiagramRenderFormat.Bitmap)
            .TryAddDiagramRenderer<MermaidComponentDiagramRenderer>(DiagramKind.Component, DiagramRenderFormat.Mermaid)
            .TryAddDiagramRenderer<SvgComponentDiagramRenderer>(DiagramKind.Component, DiagramRenderFormat.Svg)
            .TryAddDiagramRenderer<BitmapDiagramRenderer>(DiagramKind.Component, DiagramRenderFormat.Bitmap);
    }

    /// <summary>
    /// Adds a renderer registration for a specific diagram kind using Mermaid output.
    /// </summary>
    /// <typeparam name="TRenderer">The renderer type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="kind">The diagram kind handled by <typeparamref name="TRenderer"/>.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a different renderer is already registered for <paramref name="kind"/>.</exception>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRenderer&lt;MermaidSequenceDiagramRenderer&gt;(DiagramKind.Sequence);
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var renderer = factory.GetRenderer(DiagramKind.Sequence, DiagramRenderFormat.Mermaid);
    /// </code>
    /// </example>
    public static IServiceCollection AddDiagramRenderer<TRenderer>(this IServiceCollection services, DiagramKind kind)
        where TRenderer : class, IDiagramRenderer
    {
        ArgumentNullException.ThrowIfNull(services);

        return AddDiagramRendererCore<TRenderer>(services, kind, DiagramRenderFormat.Mermaid, throwOnConflict: true);
    }

    /// <summary>
    /// Adds a renderer registration for a specific diagram kind and format.
    /// </summary>
    /// <typeparam name="TRenderer">The renderer type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="kind">The diagram kind handled by <typeparamref name="TRenderer"/>.</param>
    /// <param name="format">The output format handled by <typeparamref name="TRenderer"/>.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a different renderer is already registered for <paramref name="kind"/> and <paramref name="format"/>.</exception>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddDiagramRenderer&lt;MySvgStateRenderer&gt;(DiagramKind.State, DiagramRenderFormat.Svg);
    /// var provider = services.BuildServiceProvider();
    /// var factory = provider.GetRequiredService&lt;IDiagramRendererFactory&gt;();
    /// var renderer = factory.GetRenderer(DiagramKind.State, DiagramRenderFormat.Svg);
    /// </code>
    /// </example>
    public static IServiceCollection AddDiagramRenderer<TRenderer>(this IServiceCollection services, DiagramKind kind, DiagramRenderFormat format)
        where TRenderer : class, IDiagramRenderer
    {
        ArgumentNullException.ThrowIfNull(services);

        return AddDiagramRendererCore<TRenderer>(services, kind, format, throwOnConflict: true);
    }

    private static IServiceCollection TryAddDiagramRenderer<TRenderer>(this IServiceCollection services, DiagramKind kind, DiagramRenderFormat format)
        where TRenderer : class, IDiagramRenderer
    {
        return AddDiagramRendererCore<TRenderer>(services, kind, format, throwOnConflict: false);
    }

    private static IServiceCollection AddDiagramRendererCore<TRenderer>(IServiceCollection services, DiagramKind kind, DiagramRenderFormat format, bool throwOnConflict)
        where TRenderer : class, IDiagramRenderer
    {
        var registrations = GetOrAddRegistrationStore(services);
        if (!registrations.TryAdd(kind, format, typeof(TRenderer), out var existingRendererType))
        {
            if (existingRendererType == typeof(TRenderer) || !throwOnConflict)
            {
                services.TryAddSingleton<IDiagramRendererFactory, DiagramRendererFactory>();
                return services;
            }

            throw new InvalidOperationException(
                $"Diagram kind '{kind}' is already mapped to renderer '{existingRendererType.Name}' for format '{format}'.");
        }

        services.TryAddSingleton<TRenderer>();
        services.TryAddSingleton<IDiagramRendererFactory, DiagramRendererFactory>();

        return services;
    }

    private static DiagramRendererRegistrationStore GetOrAddRegistrationStore(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(DiagramRendererRegistrationStore));
        if (descriptor?.ImplementationInstance is DiagramRendererRegistrationStore registrations)
        {
            return registrations;
        }

        registrations = new DiagramRendererRegistrationStore();
        services.TryAddSingleton(registrations);
        return registrations;
    }
}