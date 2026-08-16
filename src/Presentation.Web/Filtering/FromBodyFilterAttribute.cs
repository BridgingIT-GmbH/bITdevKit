// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Identifies a declaration with from body filter metadata.
/// </summary>
public class FromBodyFilterAttribute()
    : ModelBinderAttribute(typeof(FromBodyFilterModelBinder)),
    IBindingSourceMetadata, IFromBodyMetadata
{
    /// <inheritdoc/>
    public override BindingSource BindingSource => BindingSource.Body;

    /// <summary>
    /// Gets the allow empty.
    /// </summary>
    public bool AllowEmpty => true;
}
