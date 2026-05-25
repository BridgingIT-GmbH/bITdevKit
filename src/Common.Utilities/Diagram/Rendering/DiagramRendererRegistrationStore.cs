// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal sealed class DiagramRendererRegistrationStore
{
    private readonly Dictionary<(DiagramKind Kind, DiagramRenderFormat Format), Type> rendererTypes = [];

    public IReadOnlyList<DiagramRenderFormat> GetFormats(DiagramKind kind)
    {
        return this.rendererTypes.Keys
            .Where(key => key.Kind == kind)
            .Select(key => key.Format)
            .Distinct()
            .OrderBy(format => format)
            .ToArray();
    }

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

    public bool TryGetRendererType(DiagramKind kind, DiagramRenderFormat format, out Type rendererType)
    {
        return this.rendererTypes.TryGetValue((kind, format), out rendererType);
    }
}