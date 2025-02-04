// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ISerializer = BridgingIT.DevKit.Common.ISerializer;

public class FromQueryFilterModelBinder : IModelBinder
{
    private static readonly ISerializer Serializer = new SystemTextJsonSerializer();

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var json = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;

        if (string.IsNullOrEmpty(json))
        {
            bindingContext.Result = ModelBindingResult.Failed();

            return Task.CompletedTask;
        }

        try
        {
            var filter = Serializer.Deserialize<FilterModel>(json); // options include necessary converteres
            bindingContext.Result = ModelBindingResult.Success(filter);
        }
        catch (JsonException ex)
        {
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName,
                $"Invalid filter model format: {ex.Message}");
            bindingContext.Result = ModelBindingResult.Failed();
        }
        catch (Exception ex)
        {
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName,
                $"Error processing filter model: {ex.Message}");
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }
}