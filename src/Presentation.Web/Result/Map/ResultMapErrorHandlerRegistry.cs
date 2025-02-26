namespace Company.Project.Modules.Core.Presentation;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Registry for custom error handlers used by ResultHttpExtensions.
/// </summary>
public static class ResultMapErrorHandlerRegistry
{
#pragma warning disable IDE1006 // Naming Styles
    private static readonly Dictionary<Type, Func<ILogger, Result, IResult>> errorHandlers = new();
#pragma warning restore IDE1006 // Naming Styles

    /// <summary>
    /// Registers a custom error handler for a specific error type.
    /// </summary>
    /// <typeparam name="TError">The type of error to register a handler for.</typeparam>
    /// <param name="handler">A function that takes a logger and result and returns an IResult.</param>
    /// <remarks>
    /// If a handler is already registered for the error type, it will be replaced (last registration wins).
    /// </remarks>
    public static void RegisterHandler<TError>(Func<ILogger, Result, IResult> handler)
        where TError : IResultError
    {
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));
        errorHandlers[typeof(TError)] = handler;
    }

    /// <summary>
    /// Removes a custom error handler for a specific error type.
    /// </summary>
    /// <typeparam name="TError">The type of error to remove the handler for.</typeparam>
    /// <returns>true if the handler was removed; otherwise, false.</returns>
    public static bool RemoveHandler<TError>()
        where TError : IResultError
    {
        return errorHandlers.Remove(typeof(TError));
    }

    /// <summary>
    /// Clears all registered error handlers.
    /// </summary>
    public static void ClearHandlers()
    {
        errorHandlers.Clear();
    }

    /// <summary>
    /// Checks if a handler is registered for the specified error type.
    /// </summary>
    /// <typeparam name="TError">The type of error to check.</typeparam>
    /// <returns>true if a handler is registered for the specified error type; otherwise, false.</returns>
    public static bool HasHandlerFor<TError>()
        where TError : IResultError
    {
        return errorHandlers.ContainsKey(typeof(TError));
    }

    /// <summary>
    /// Gets a list of all error types with registered handlers.
    /// </summary>
    /// <returns>An enumerable of all registered error types.</returns>
    public static IEnumerable<Type> GetRegisteredErrorTypes()
    {
        return errorHandlers.Keys;
    }

    /// <summary>
    /// Attempts to find a custom handler for any error in the provided result.
    /// </summary>
    /// <param name="result">The result containing errors to look for handlers for.</param>
    /// <param name="logger">The logger to pass to the handler.</param>
    /// <param name="customResult">When this method returns, contains the result from the custom handler if found; otherwise, null.</param>
    /// <returns>true if a custom handler was found and executed; otherwise, false.</returns>
    internal static bool TryExecuteCustomHandler(Result result, ILogger logger, out IResult customResult)
    {
        customResult = null;

        if (result.IsFailure && result.Errors != null)
        {
            foreach (var error in result.Errors)
            {
                var errorType = error.GetType();
                if (errorHandlers.TryGetValue(errorType, out var handler))
                {
                    customResult = handler(logger, result);
                    return true;
                }
            }
        }

        return false;
    }
}