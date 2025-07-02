// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Windows;

using System;

public interface IWindowsImpersonationService : IDisposable
{
    /// <summary>
    /// Executes an operation using Windows impersonation with the configured credentials.
    /// </summary>
    /// <typeparam name="T">The type of result returned by the operation.</typeparam>
    /// <param name="operation">The operation to execute under impersonation.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteImpersonatedAsync<T>(Func<Task<T>> operation);

    /// <summary>
    /// Executes an operation using Windows impersonation with the configured credentials.
    /// </summary>
    /// <param name="operation">The operation to execute under impersonation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteImpersonatedAsync(Func<Task> operation);
}
