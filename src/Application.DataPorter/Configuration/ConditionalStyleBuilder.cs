// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Builder for configuring conditional styles.
/// </summary>
public sealed class ConditionalStyleBuilder
{
    internal bool IsBold { get; private set; }
    internal bool IsItalic { get; private set; }
    internal string ForegroundColor { get; private set; }
    internal string BackgroundColor { get; private set; }

    /// <summary>
    /// Makes the text bold.
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    public ConditionalStyleBuilder Bold()
    {
        this.IsBold = true;
        return this;
    }

    /// <summary>
    /// Makes the text italic.
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    public ConditionalStyleBuilder Italic()
    {
        this.IsItalic = true;
        return this;
    }

    /// <summary>
    /// Sets the foreground (text) color.
    /// </summary>
    /// <param name="color">The color in hex format (e.g., "#FF0000").</param>
    /// <returns>This builder for method chaining.</returns>
    public ConditionalStyleBuilder WithForegroundColor(string color)
    {
        this.ForegroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets the background color.
    /// </summary>
    /// <param name="color">The color in hex format (e.g., "#FFFF00").</param>
    /// <returns>This builder for method chaining.</returns>
    public ConditionalStyleBuilder WithBackgroundColor(string color)
    {
        this.BackgroundColor = color;
        return this;
    }
}
