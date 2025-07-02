// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

public class FromQueryFilterAttribute()
    : ModelBinderAttribute(typeof(FromQueryFilterModelBinder))
    , IBindingSourceMetadata, IModelNameProvider, IFromQueryMetadata
{
    public override BindingSource BindingSource => BindingSource.Query;
}