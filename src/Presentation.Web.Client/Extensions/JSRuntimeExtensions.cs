// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.JSInterop;

[ExcludeFromCodeCoverage]
public static class JsRuntimeExtensions
{
    /// <summary>
    /// Calls "console.log" on the client passing the args along with it.
    /// </summary>
    /// <example>
    /// LogAsync("data") //same as console.log('data')
    /// </example>
    /// <example>
    /// LogAsync("data", myData) //same as console.log('data', myData)
    /// </example>
    public static async Task LogAsync(this IJSRuntime source, params object[] args)
    {
        await source.InvokeVoidAsync("console.log", args);
    }

    /// <summary>
    /// Calls "console.table" on the client passing the args along with it.
    /// </summary>
    /// <example>
    /// TableAsync(myData) //same as console.table(data)
    /// </example>
    /// <example>
    /// TableAsync(myData, new []{"firstName", "lastName"}) //same as console.table(myData, ["firstName", "lastName"])
    /// </example>
    public static async Task TableAsync(this IJSRuntime source, object data, string[] fields = null)
    {
        await source.InvokeVoidAsync("console.table", data, fields);
    }

    /// <summary>
    /// Set the provided object to a global variable.
    /// </summary>
    /// <example>
    /// SetGlobalAsync("foo", myData) //same as window.foo = myData
    /// </example>
    public static async Task SetGlobalAsync(this IJSRuntime source, string name, object data)
    {
        await source.InvokeVoidAsync("setGlobal", name, JsonSerializer.Serialize(data));
    }

    /// <summary>
    /// Calls "navigator.clipboard.writeText" on the client passing the string along with it.
    /// </summary>
    /// <example>
    /// CopyToClipboardAsync("data") //same as navigator.clipboard.writeText('data')
    /// </example>
    public static async Task CopyToClipboardAsync(this IJSRuntime source, string text)
    {
        await source.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }
}