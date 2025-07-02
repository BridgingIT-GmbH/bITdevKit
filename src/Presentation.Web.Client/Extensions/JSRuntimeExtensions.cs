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
    ///     Calls "console.log" on the client passing the args along with it.
    /// </summary>
    /// <example>
    ///     LogAsync("data") //same as console.log('data')
    /// </example>
    /// <example>
    ///     LogAsync("data", myData) //same as console.log('data', myData)
    /// </example>
    public static async Task ConsoleLogAsync(this IJSRuntime source, params object[] args)
    {
        try
        {
            await source.InvokeVoidAsync("console.log", args);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    /// <summary>
    ///     Calls "console.log" on the client passing the args along with it.
    /// </summary>
    /// <example>
    ///     LogAsync("data") //same as console.log('data')
    /// </example>
    /// <example>
    ///     LogAsync("data", myData) //same as console.log('data', myData)
    /// </example>
    public static async Task ConsoleInfoAsync(this IJSRuntime source, params object[] args)
    {
        try
        {
            await source.InvokeVoidAsync("console.info", args);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    /// <summary>
    ///     Calls "console.log" on the client passing the args along with it.
    /// </summary>
    /// <example>
    ///     LogAsync("data") //same as console.log('data')
    /// </example>
    /// <example>
    ///     LogAsync("data", myData) //same as console.log('data', myData)
    /// </example>
    public static async Task ConsoleWarnAsync(this IJSRuntime source, params object[] args)
    {
        try
        {
            await source.InvokeVoidAsync("console.warn", args);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    /// <summary>
    ///     Calls "console.log" on the client passing the args along with it.
    /// </summary>
    /// <example>
    ///     LogAsync("data") //same as console.log('data')
    /// </example>
    /// <example>
    ///     LogAsync("data", myData) //same as console.log('data', myData)
    /// </example>
    public static async Task ConsoleErrorAsync(this IJSRuntime source, params object[] args)
    {
        try
        {
            await source.InvokeVoidAsync("console.error", args);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    /// <summary>
    ///     Calls "console.log" on the client passing the args along with it.
    /// </summary>
    /// <example>
    ///     LogAsync("data") //same as console.log('data')
    /// </example>
    /// <example>
    ///     LogAsync("data", myData) //same as console.log('data', myData)
    /// </example>
    public static async Task ConsoleExceptionAsync(this IJSRuntime source, Exception exception)
    {
        try
        {
            await source.InvokeVoidAsync("console.error", $"Exception: {exception}");
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
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
        try
        {
            await source.InvokeVoidAsync("console.table", data, fields);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    /// <summary>
    ///     Set the provided object to a global variable.
    /// </summary>
    /// <example>
    ///     SetGlobalAsync("foo", myData) //same as window.foo = myData
    /// </example>
    public static async Task SetGlobalAsync(this IJSRuntime source, string name, object data)
    {
        try
        {
            await source.InvokeVoidAsync("setGlobal", name, JsonSerializer.Serialize(data));
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    /// <summary>
    ///     Calls "navigator.clipboard.writeText" on the client passing the string along with it.
    /// </summary>
    /// <example>
    ///     CopyToClipboardAsync("data") //same as navigator.clipboard.writeText('data')
    /// </example>
    public static async Task CopyToClipboardAsync(this IJSRuntime source, string text)
    {
        try
        {
            await source.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    public static async Task NavigateTo(this IJSRuntime source, string url, string target = "_blank")
    {
        try
        {
            await source.InvokeVoidAsync("window.open", url, target);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
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

    public static async ValueTask<string> GetLocalStorageItemAsync(this IJSRuntime source, string key)
    {
        try
        {
            return await source.InvokeAsync<string>("localStorage.getItem", key);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }

        return default;
    }

    public static async ValueTask<T> GetLocalStorageItemAsync<T>(this IJSRuntime source, string key)
    {
        var json = await source.GetLocalStorageItemAsync(key);
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public static async ValueTask SetLocalStorageItemAsync(this IJSRuntime source, string key, string value)
    {
        try
        {
            await source.InvokeVoidAsync("localStorage.setItem", key, value);
            return;
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    public static ValueTask SetLocalStorageItemAsync<T>(this IJSRuntime source, string key, T value)
    {
        return source.SetLocalStorageItemAsync(key, JsonSerializer.Serialize(value));
    }

    public static async ValueTask RemoveLocalStorageItemAsync(this IJSRuntime source, string key)
    {
        try
        {
            await source.InvokeVoidAsync("localStorage.removeItem", key);
            return;
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    public static async ValueTask ClearLocalStorageAsync(this IJSRuntime source)
    {
        try
        {
            await source.InvokeVoidAsync("localStorage.clear");
            return;
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    public static async ValueTask<int> GetLocalStorageItemCountAsync(this IJSRuntime source)
    {
        try
        {
            return await source.InvokeAsync<int>("eval", "localStorage.length");
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }

        return default;
    }

    public static async ValueTask<string> GetSessionStorageItemAsync(this IJSRuntime source, string key)
    {
        try
        {
            return await source.InvokeAsync<string>("sessionStorage.getItem", key);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }

        return default;
    }

    public static async ValueTask<T> GetSessionStorageItemAsync<T>(this IJSRuntime source, string key)
    {
        var json = await source.GetSessionStorageItemAsync(key);
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public static async ValueTask SetSessionStorageItemAsync(this IJSRuntime source, string key, string value)
    {
        try
        {
            await source.InvokeVoidAsync("sessionStorage.setItem", key, value);
            return;
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    public static ValueTask SetSessionStorageItemAsync<T>(this IJSRuntime source, string key, T value)
    {
        return source.SetSessionStorageItemAsync(key, JsonSerializer.Serialize(value));
    }

    public static async ValueTask RemoveSessionStorageItemAsync(this IJSRuntime source, string key)
    {
        try
        {
            await source.InvokeVoidAsync("sessionStorage.removeItem", key);
            return;
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    public static async ValueTask ClearSessionStorageAsync(this IJSRuntime source)
    {
        try
        {
            await source.InvokeVoidAsync("sessionStorage.clear");
            return;
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    public static ValueTask<int> GetSessionStorageItemCountAsync(this IJSRuntime source)
    {
        return source.InvokeAsync<int>("eval", "sessionStorage.length");
    }

    public static async ValueTask<bool> HasStorageKeyAsync(this IJSRuntime source, string key, bool useSessionStorage = false)
    {
        var storageType = useSessionStorage ? "sessionStorage" : "localStorage";

        try
        {
            return await source.InvokeAsync<bool>("eval", $"{storageType}.hasOwnProperty('{key}')");
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }

        return false;
    }

    public static async ValueTask<string[]> GetStorageKeysAsync(this IJSRuntime source, bool useSessionStorage = false)
    {
        var storageType = useSessionStorage ? "sessionStorage" : "localStorage";

        try
        {
            return await source.InvokeAsync<string[]>("eval", $"Object.keys({storageType})");
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }

        return [];
    }
}