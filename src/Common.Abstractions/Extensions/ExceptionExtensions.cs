// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Data.Common;
using System.Diagnostics;
using System.Net.Sockets;

/// <summary>Provides helpers for inspecting exceptions.</summary>
/// <example><code>var message = exception.GetFullMessage();</code></example>
public static class ExceptionExtensions
{
    /// <param name="source">The exception to format.</param>
    extension(Exception source)
    {
        /// <summary>Builds a flattened message that includes the exception type and nested inner exceptions.</summary>
        /// <returns>The formatted exception message, or null when <paramref name="source" /> is null.</returns>
        /// <example><code>var message = exception.GetFullMessage();</code></example>
        [DebuggerStepThrough]
        public string GetFullMessage()
        {
            if (source is null)
            {
                return null;
            }

            return source?.InnerException is null
                ? $"[{source.GetType().Name}] {source?.Message}".Replace(Environment.NewLine, Environment.NewLine + " ")
                : $"[{source.GetType().Name}] {source.Message}  --> {source.InnerException.GetFullMessage()}".Replace(
                    Environment.NewLine,
                    Environment.NewLine + " ");
        }

        /// <summary>
        ///     Determines whether the given <paramref name="source" /> is of type <typeparamref name="TEx" />.
        /// </summary>
        [DebuggerStepThrough]
        public bool IsExpectedException<TEx>()
            where TEx : Exception
        {
            return source.IsExpectedException(e => e is TEx);
        }

        /// <summary>
        ///     Determines whether the given <paramref name="source" /> is any of the types <typeparamref name="TEx1" />
        ///     or <typeparamref name="TEx2" />.
        /// </summary>
        [DebuggerStepThrough]
        public bool IsExpectedException<TEx1, TEx2>()
            where TEx1 : Exception
            where TEx2 : Exception
        {
            return source.IsExpectedException(e => e is TEx1 || e is TEx2);
        }

        /// <summary>
        ///     Determines whether the given <paramref name="source" /> is any of the types <typeparamref name="TEx1" />,
        ///     <typeparamref name="TEx2" /> or <typeparamref name="TEx3" />.
        /// </summary>
        [DebuggerStepThrough]
        public bool IsExpectedException<TEx1, TEx2, TEx3>()
            where TEx1 : Exception
            where TEx2 : Exception
            where TEx3 : Exception
        {
            return source.IsExpectedException(e => e is TEx1 || e is TEx2 || e is TEx3);
        }

        /// <summary>
        ///     Determines whether the given <paramref name="source" /> is any of the types <typeparamref name="TEx1" />,
        ///     <typeparamref name="TEx2" />, <typeparamref name="TEx3" /> or <typeparamref name="TEx4" />.
        /// </summary>
        [DebuggerStepThrough]
        public bool IsExpectedException<TEx1, TEx2, TEx3, TEx4>()
            where TEx1 : Exception
            where TEx2 : Exception
            where TEx3 : Exception
            where TEx4 : Exception
        {
            return source.IsExpectedException(e => e is TEx1 || e is TEx2 || e is TEx3 || e is TEx4);
        }

        /// <summary>
        ///     Determines whether the given <paramref name="source" /> is any of the types <typeparamref name="TEx1" />,
        ///     <typeparamref name="TEx2" />, <typeparamref name="TEx3" />, <typeparamref name="TEx4" />
        ///     or <typeparamref name="TEx5" />.
        /// </summary>
        [DebuggerStepThrough]
        public bool IsExpectedException<TEx1, TEx2, TEx3, TEx4, TEx5>()
            where TEx1 : Exception
            where TEx2 : Exception
            where TEx3 : Exception
            where TEx4 : Exception
            where TEx5 : Exception
        {
            return source.IsExpectedException(e => e is TEx1 || e is TEx2 || e is TEx3 || e is TEx4 || e is TEx5);
        }

        /// <summary>
        ///     Determines whether the given <paramref name="source" /> is any of the types <typeparamref name="TEx1" />,
        ///     <typeparamref name="TEx2" />, <typeparamref name="TEx3" />, <typeparamref name="TEx4" />,
        ///     <typeparamref name="TEx5" /> or <typeparamref name="TEx6" />.
        /// </summary>
        [DebuggerStepThrough]
        public bool IsExpectedException<TEx1, TEx2, TEx3, TEx4, TEx5, TEx6>()
            where TEx1 : Exception
            where TEx2 : Exception
            where TEx3 : Exception
            where TEx4 : Exception
            where TEx5 : Exception
            where TEx6 : Exception
        {
            return source.IsExpectedException(e => e is TEx1 || e is TEx2 || e is TEx3 || e is TEx4 || e is TEx5 || e is TEx6);
        }

        /// <summary>
        ///     Determines whether the given <paramref name="source" /> is any of the types <typeparamref name="TEx1" />,
        ///     <typeparamref name="TEx2" />, <typeparamref name="TEx3" />, <typeparamref name="TEx4" />,
        ///     <typeparamref name="TEx5" />, <typeparamref name="TEx6" /> or <typeparamref name="TEx7" />.
        /// </summary>
        [DebuggerStepThrough]
        public bool IsExpectedException<TEx1, TEx2, TEx3, TEx4, TEx5, TEx6, TEx7>()
            where TEx1 : Exception
            where TEx2 : Exception
            where TEx3 : Exception
            where TEx4 : Exception
            where TEx5 : Exception
            where TEx6 : Exception
            where TEx7 : Exception
        {
            return source.IsExpectedException(e =>
                e is TEx1 || e is TEx2 || e is TEx3 || e is TEx4 || e is TEx5 || e is TEx6 || e is TEx7);
        }

        /// <summary>
        ///     Determines whether the given <paramref name="source" /> is the type matched by <paramref name="predicate" />.
        /// </summary>
        [DebuggerStepThrough]
        public bool IsExpectedException(Func<Exception, bool> predicate)
        {
            if (predicate(source))
            {
                return true;
            }

            if (source is AggregateException aggEx)
            {
                var found = false;
                aggEx.Flatten()
                    .Handle(x =>
                    {
                        if (predicate(x))
                        {
                            found = true;
                        }

                        return true;
                    });

                return found;
            }

            return false;
        }

        /// <summary>
        ///    Determines whether the given <paramref name="source" /> is a transient exception.
        ///    What are transient errors: https://docs.microsoft.com/en-us/azure/architecture/best-practices/transient-faults
        /// </summary>
        /// <returns></returns>
        public bool IsTransientException() =>
            source is DbException or SocketException or HttpRequestException or TaskCanceledException or TimeoutException;
    }

    // || ex is SqlException || ex is RequestFailedException
}
