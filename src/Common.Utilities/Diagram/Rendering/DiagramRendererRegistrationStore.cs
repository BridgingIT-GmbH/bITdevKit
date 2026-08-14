// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Maintains the renderer registrations for different diagram kinds and render formats.
/// </summary>
public sealed class DiagramRendererRegistrationStore
{
    private readonly Dictionary<(DiagramKind Kind, DiagramRenderFormat Format), Type> rendererTypes = [];

    /// <summary>
    /// Gets the render formats that have been registered for the specified diagram kind.
    /// </summary>
    /// <param name="kind">The diagram kind whose registered render formats are requested.</param>
    /// <returns>
    /// The unique render formats associated with the specified kind, ordered by their value.
    /// </returns>
    public IReadOnlyList<DiagramRenderFormat> GetFormats(DiagramKind kind)
    {
        return this.rendererTypes.Keys
            .Where(key => key.Kind == kind)
            .Select(key => key.Format)
            .Distinct()
            .OrderBy(format => format)
            .ToArray();
    }

    /// <summary>
    /// Registers a renderer type for the specified diagram kind and output format.
    /// </summary>
    /// <param name="kind">The diagram kind for which the renderer is registered.</param>
    /// <param name="format">The render format the renderer produces.</param>
    /// <param name="rendererType">The renderer implementation type to register.</param>
    /// <param name="existingRendererType">
    /// When a renderer is already registered for the same kind and format, contains the existing type; otherwise, null.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the renderer was registered; otherwise, <see langword="false"/> when a renderer already exists for the same key.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rendererType"/> is <see langword="null"/>.</exception>
    public bool TryAdd(DiagramKind kind, DiagramRenderFormat format, Type rendererType, out Type existingRendererType)
    {
        ArgumentNullException.ThrowIfNull(rendererType);

        if (this.rendererTypes.TryGetValue((kind, format), out existingRendererType))
        {
            return false;
        }

        this.rendererTypes[(kind, format)] = rendererType;
        existingRendererType = null;
        return true;
    }

    /// <summary>
    /// Attempts to get the registered renderer type for the specified diagram kind and render format.
    /// </summary>
    /// <param name="kind">The diagram kind whose renderer is requested.</param>
    /// <param name="format">The render format whose renderer is requested.</param>
    /// <param name="rendererType">
    /// When found, the registered renderer type; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a renderer is registered for the specified key; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetRendererType(DiagramKind kind, DiagramRenderFormat format, out Type rendererType)
    {
        return this.rendererTypes.TryGetValue((kind, format), out rendererType);
    }
}