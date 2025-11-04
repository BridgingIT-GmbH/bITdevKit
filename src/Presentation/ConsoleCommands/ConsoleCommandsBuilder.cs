// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation;

/// <summary>
/// Fluent builder for interactive console command registration.
/// </summary>
/// <remarks>Initializes builder with the provided service collection.</remarks>
public class ConsoleCommandsBuilder(IServiceCollection services)
{
    /// <summary>Gets the DI service collection used for registrations.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Adds a transient console command implementation.
    /// </summary>
    /// <typeparam name="TCommand">The command type implementing <see cref="IConsoleCommand"/>.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public ConsoleCommandsBuilder AddCommand<TCommand>() where TCommand : class, IConsoleCommand
    {
        this.Services.AddTransient<IConsoleCommand, TCommand>();

        return this;
    }
}