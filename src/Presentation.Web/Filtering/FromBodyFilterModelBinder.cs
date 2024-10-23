// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;

public class FromBodyFilterModelBinder : IModelBinder
{
    private static readonly ISerializer Serializer = new SystemTextJsonSerializer();

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        using var reader = new StreamReader(bindingContext.HttpContext.Request.Body);
        var json = await reader.ReadToEndAsync();

        if (string.IsNullOrEmpty(json))
        {
            bindingContext.Result = ModelBindingResult.Failed();

            return;
        }

        try
        {
            var filterModel = Serializer.Deserialize<FilterModel>(json); // options include necessary converteres
            bindingContext.Result = ModelBindingResult.Success(filterModel);
        }
        catch (JsonException)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Invalid JSON format for filter model.");
            bindingContext.Result = ModelBindingResult.Failed();
        }
    }
}