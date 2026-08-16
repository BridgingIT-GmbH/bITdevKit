// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Shouldly;

/// <summary>
///     Provides Shouldly assertions for DevKit result state, messages, errors, and values.
/// </summary>
public static class ShouldBeResultExtensions
{
    /// <summary>Asserts that a result is successful.</summary>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">The result is not successful.</exception>
    [DebuggerStepThrough]
    public static void ShouldBeSuccess([DoesNotReturnIf(false)] this IResult actual, string customMessage = null)
    {
        if (!actual.IsSuccess)
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(true, actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a result is unsuccessful.</summary>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">The result is successful.</exception>
    [DebuggerStepThrough]
    public static void ShouldBeFailure([DoesNotReturnIf(false)] this IResult actual, string customMessage = null)
    {
        if (actual.IsSuccess)
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(false, actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a result's message collection contains an exact message.</summary>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="expected">The exact message expected.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">A non-null message collection does not contain the expected message.</exception>
    [DebuggerStepThrough]
    public static void ShouldContainMessage(
        [DoesNotReturnIf(false)] this IResult actual,
        string expected,
        string customMessage = null)
    {
        if (actual.Messages?.Contains(expected) == false)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a result's message collection does not contain an exact message.</summary>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="expected">The exact message that must be absent.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">The message collection contains the message.</exception>
    [DebuggerStepThrough]
    public static void ShouldNotContainMessage(
        [DoesNotReturnIf(false)] this IResult actual,
        string expected,
        string customMessage = null)
    {
        if (actual.Messages?.Contains(expected) == true)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a result contains at least one message.</summary>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">The result has no messages.</exception>
    [DebuggerStepThrough]
    public static void ShouldContainMessages([DoesNotReturnIf(false)] this IResult actual, string customMessage = null)
    {
        if (actual.Messages.IsNullOrEmpty())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(true, actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a result contains no messages.</summary>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">The result has one or more messages.</exception>
    [DebuggerStepThrough]
    public static void ShouldNotContainMessages(
        [DoesNotReturnIf(false)] this IResult actual,
        string customMessage = null)
    {
        if (!actual.Messages.IsNullOrEmpty())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(false, actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a result contains an error of a specified type.</summary>
    /// <typeparam name="TError">The error type expected.</typeparam>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">No error of the specified type exists.</exception>
    [DebuggerStepThrough]
    public static void ShouldContainError<TError>(
        [DoesNotReturnIf(false)] this IResult actual,
        string customMessage = null)
        where TError : class, IResultError
    {
        if (!actual.HasError<TError>())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(typeof(TError), actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a result does not contain an error of a specified type.</summary>
    /// <typeparam name="TError">The error type that must be absent.</typeparam>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">An error of the specified type exists.</exception>
    [DebuggerStepThrough]
    public static void ShouldNotContainError<TError>(
        [DoesNotReturnIf(false)] this IResult actual,
        string customMessage = null)
        where TError : class, IResultError
    {
        if (actual.HasError<TError>())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(typeof(TError), actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a non-null result value equals an expected value.</summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">The current non-null value does not equal the expected value.</exception>
    [DebuggerStepThrough]
    public static void ShouldBeValue<T>(
        [DoesNotReturnIf(false)] this Result<T> actual,
        T expected,
        string customMessage = null)
    {
        if (actual.Value?.Equals(expected) == false)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }

    /// <summary>Asserts that a result value does not equal an expected value.</summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="actual">The result to inspect.</param>
    /// <param name="expected">The value that must not match.</param>
    /// <param name="customMessage">An optional assertion message.</param>
    /// <exception cref="ShouldAssertException">The current value equals the specified value.</exception>
    [DebuggerStepThrough]
    public static void ShouldNotBeValue<T>(
        [DoesNotReturnIf(false)] this Result<T> actual,
        T expected,
        string customMessage = null)
    {
        if (actual.Value?.Equals(expected) == true)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }
}
