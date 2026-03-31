// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes an inline step delegate stored in a built pipeline definition.
/// </summary>
public class PipelineInlineStepDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineInlineStepDescriptor"/> class.
    /// </summary>
    /// <param name="contextType">The context type expected by the inline handler.</param>
    /// <param name="isAsync">A value indicating whether the handler is asynchronous.</param>
    /// <param name="handler">The inline handler delegate.</param>
    public PipelineInlineStepDescriptor(
        Type contextType,
        bool isAsync,
        Delegate handler)
    {
        this.ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
        this.IsAsync = isAsync;
        this.Handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// Gets the context type expected by the inline handler.
    /// </summary>
    public Type ContextType { get; }

    /// <summary>
    /// Gets a value indicating whether the handler is asynchronous.
    /// </summary>
    public bool IsAsync { get; }

    /// <summary>
    /// Gets the inline handler delegate.
    /// </summary>
    public Delegate Handler { get; }
}
