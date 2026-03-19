// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Defines the annotation style used when generating templates.
/// </summary>
public enum TemplateAnnotationStyle
{
    /// <summary>
    /// Generates a structure-only template without annotation hints.
    /// </summary>
    StructureOnly,

    /// <summary>
    /// Generates an annotated template with field hints and metadata.
    /// </summary>
    Annotated
}
