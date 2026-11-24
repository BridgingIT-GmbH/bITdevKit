// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>Records explicitly assigned option properties without assigning values for omitted properties.</summary>
/// <typeparam name="TOptions">The JSON-serializable option data type.</typeparam>
/// <typeparam name="TSelf">The concrete fluent builder type.</typeparam>
/// <remarks>Feature builders expose specific methods which record assignments through <see cref="With{TValue}"/>.
/// The feature decides when to apply its patches and configuration overlays.</remarks>
/// <example><code>builder.With(options => options.Enabled, false);</code></example>
public abstract class PatchOptionsBuilder<TOptions, TSelf>
    where TOptions : class, new()
    where TSelf : PatchOptionsBuilder<TOptions, TSelf>
{
    /// <summary>Gets the explicitly recorded assignments, including false, zero, null, and empty collections.</summary>
    /// <example><code>var options = builder.Patch.Apply(new FeatureOptions(), "Module", provenance);</code></example>
    public OptionsPatch Patch { get; } = new();

    /// <summary>Records one property assignment, including a nested property selected without computation or indexers.</summary>
    /// <typeparam name="TValue">The JSON-serializable property value type.</typeparam>
    /// <param name="property">A direct property path rooted at the option parameter.</param>
    /// <param name="value">The explicit value to snapshot.</param>
    /// <returns>This concrete builder for further configuration.</returns>
    /// <example><code>builder.With(options => options.Retry.Attempts, 0);</code></example>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public TSelf With<TValue>(Expression<Func<TOptions, TValue>> property, TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);
        var names = new Stack<string>();
        var expression = property.Body;
        while (expression is MemberExpression member && member.Member is PropertyInfo info)
        {
            names.Push(info.Name);
            expression = member.Expression;
        }

        if (expression != property.Parameters[0] || names.Count == 0)
        {
            throw new ArgumentException("Select one option property without computation or indexers.", nameof(property));
        }

        this.Patch.Set(string.Join('.', names), value);
        return (TSelf)this;
    }
}