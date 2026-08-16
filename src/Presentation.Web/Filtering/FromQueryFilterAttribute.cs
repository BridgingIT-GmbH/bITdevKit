// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Identifies a declaration with from query filter metadata.
/// </summary>
public class FromQueryFilterAttribute()
    : ModelBinderAttribute(typeof(FromQueryFilterModelBinder))
    , IBindingSourceMetadata, IModelNameProvider, IFromQueryMetadata
{
    /// <inheritdoc/>
    public override BindingSource BindingSource => BindingSource.Query;
}
