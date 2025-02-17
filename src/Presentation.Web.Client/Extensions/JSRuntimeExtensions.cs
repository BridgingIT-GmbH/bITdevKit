// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.JSInterop;
using static System.Net.Mime.MediaTypeNames;

[ExcludeFromCodeCoverage]
public static class JsRuntimeExtensions
{
    /// <summary>
    ///     Calls "console.log" on the client passing the args along with it.
    /// </summary>
    /// <example>
    ///     LogAsync("data") //same as console.log('data')
    /// </example>
    /// <example>
    ///     LogAsync("data", myData) //same as console.log('data', myData)
    /// </example>
    public static async Task LogAsync(this IJSRuntime source, params object[] args)
    {
        await source.InvokeVoidAsync("console.log", args);
    }

    /// <summary>
    ///     Calls "console.table" on the client passing the args along with it.
    /// </summary>
    /// <example>
    ///     TableAsync(myData) //same as console.table(data)
    /// </example>
    /// <example>
    ///     TableAsync(myData, new []{"firstName", "lastName"}) //same as console.table(myData, ["firstName", "lastName"])
    /// </example>
    public static async Task TableAsync(this IJSRuntime source, object data, string[] fields = null)
    {
        await source.InvokeVoidAsync("console.table", data, fields);
    }

    /// <summary>
    ///     Set the provided object to a global variable.
    /// </summary>
    /// <example>
    ///     SetGlobalAsync("foo", myData) //same as window.foo = myData
    /// </example>
    public static async Task SetGlobalAsync(this IJSRuntime source, string name, object data)
    {
        await source.InvokeVoidAsync("setGlobal", name, JsonSerializer.Serialize(data));
    }

    /// <summary>
    ///     Calls "navigator.clipboard.writeText" on the client passing the string along with it.
    /// </summary>
    /// <example>
    ///     CopyToClipboardAsync("data") //same as navigator.clipboard.writeText('data')
    /// </example>
    public static async Task CopyToClipboardAsync(this IJSRuntime source, string text)
    {
        await source.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }

    public static async Task NavigateTo(this IJSRuntime source, string url, string target = "_blank")
    {
        await source.InvokeVoidAsync("window.open", url, target);
    }

    /// <summary>
    /// Evaluates JavaScript code and returns the result.
    /// </summary>
    /// <typeparam name="T">The expected return type</typeparam>
    /// <param name="source">The IJSRuntime instance</param>
    /// <param name="code">JavaScript code to evaluate</param>
    /// <returns>The evaluated result</returns>
    /// <example>
    /// await jsRuntime.EvalAsync<string[]>("Object.keys(sessionStorage).filter(k => k.startsWith('oidc.user:'))")
    /// </example>
    public static ValueTask<T> EvalAsync<T>(this IJSRuntime source, string code)
    {
        return source.InvokeAsync<T>("eval", code);
    }

    public static ValueTask<string> GetLocalStorageItemAsync(this IJSRuntime source, string key)
    {
        return source.InvokeAsync<string>("localStorage.getItem", key);
    }

    public static async ValueTask<T> GetLocalStorageItemAsync<T>(this IJSRuntime source, string key)
    {
        var json = await source.GetLocalStorageItemAsync(key);
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public static ValueTask SetLocalStorageItemAsync(this IJSRuntime source, string key, string value)
    {
        return source.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    public static ValueTask SetLocalStorageItemAsync<T>(this IJSRuntime source, string key, T value)
    {
        return source.SetLocalStorageItemAsync(key, JsonSerializer.Serialize(value));
    }

    public static ValueTask RemoveLocalStorageItemAsync(this IJSRuntime source, string key)
    {
        return source.InvokeVoidAsync("localStorage.removeItem", key);
    }

    public static ValueTask ClearLocalStorageAsync(this IJSRuntime source)
    {
        return source.InvokeVoidAsync("localStorage.clear");
    }

    public static ValueTask<int> GetLocalStorageItemCountAsync(this IJSRuntime source)
    {
        return source.InvokeAsync<int>("eval", "localStorage.length");
    }

    public static ValueTask<string> GetSessionStorageItemAsync(this IJSRuntime source, string key)
    {
        return source.InvokeAsync<string>("sessionStorage.getItem", key);
    }

    public static async ValueTask<T> GetSessionStorageItemAsync<T>(this IJSRuntime source, string key)
    {
        var json = await source.GetSessionStorageItemAsync(key);
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public static ValueTask SetSessionStorageItemAsync(this IJSRuntime source, string key, string value)
    {
        return source.InvokeVoidAsync("sessionStorage.setItem", key, value);
    }

    public static ValueTask SetSessionStorageItemAsync<T>(this IJSRuntime source, string key, T value)
    {
        return source.SetSessionStorageItemAsync(key, JsonSerializer.Serialize(value));
    }

    public static ValueTask RemoveSessionStorageItemAsync(this IJSRuntime source, string key)
    {
        return source.InvokeVoidAsync("sessionStorage.removeItem", key);
    }

    public static ValueTask ClearSessionStorageAsync(this IJSRuntime source)
    {
        return source.InvokeVoidAsync("sessionStorage.clear");
    }

    public static ValueTask<int> GetSessionStorageItemCountAsync(this IJSRuntime source)
    {
        return source.InvokeAsync<int>("eval", "sessionStorage.length");
    }

    public static async ValueTask<bool> HasStorageKeyAsync(this IJSRuntime source, string key, bool useSessionStorage = false)
    {
        var storageType = useSessionStorage ? "sessionStorage" : "localStorage";
        return await source.InvokeAsync<bool>("eval", $"{storageType}.hasOwnProperty('{key}')");
    }

    public static async ValueTask<string[]> GetStorageKeysAsync(this IJSRuntime source, bool useSessionStorage = false)
    {
        var storageType = useSessionStorage ? "sessionStorage" : "localStorage";
        return await source.InvokeAsync<string[]>("eval", $"Object.keys({storageType})");
    }
}