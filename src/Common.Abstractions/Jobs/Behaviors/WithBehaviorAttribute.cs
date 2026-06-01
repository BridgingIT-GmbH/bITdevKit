// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Declares a job behavior on a concrete job type.
/// </summary>
/// <example>
/// <code>
/// [WithBehavior(typeof(ModuleScopeBehavior))]
/// public sealed class CleanupJob : JobBase
/// {
///     public override Task&lt;Result&gt; ExecuteAsync(
///         IJobExecutionContext&lt;Unit&gt; context,
///         CancellationToken cancellationToken = default)
///     {
///         return Task.FromResult(Result.Success());
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class WithBehaviorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithBehaviorAttribute"/> class.
    /// </summary>
    public WithBehaviorAttribute(Type behaviorType)
    {
        if (behaviorType is null)
        {
            throw new ArgumentNullException(nameof(behaviorType));
        }

        if (!typeof(IJobBehavior).IsAssignableFrom(behaviorType) || behaviorType.IsAbstract)
        {
            throw new ArgumentException($"The behavior type '{behaviorType.FullName}' must implement {nameof(IJobBehavior)}.", nameof(behaviorType));
        }

        this.BehaviorType = behaviorType;
    }

    /// <summary>
    /// Gets the declared behavior type.
    /// </summary>
    public Type BehaviorType { get; }
}