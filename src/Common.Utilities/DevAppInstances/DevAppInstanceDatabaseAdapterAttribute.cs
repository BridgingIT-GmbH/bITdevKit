// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Marks an assembly that provides a development instance database adapter. <c>AddDevAppInstance</c> adds the adapter
/// automatically when the assembly ships with the application, so hosts don't have to call
/// <c>WithSqlServerDatabases()</c> or a similar method.
/// </summary>
/// <remarks>
/// The adapter type needs a public static <c>Instance</c> property or a public parameterless constructor.
/// </remarks>
/// <example>
/// <code>
/// [assembly: DevAppInstanceDatabaseAdapter(typeof(TaskHarborDevAppInstanceAdapter))]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class DevAppInstanceDatabaseAdapterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DevAppInstanceDatabaseAdapterAttribute"/> class.
    /// </summary>
    /// <param name="adapterType">The adapter type that implements <see cref="IDevAppInstanceDatabaseAdapter"/>.</param>
    /// <example>
    /// <code>
    /// [assembly: DevAppInstanceDatabaseAdapter(typeof(SqliteDevAppInstanceAdapter))]
    /// </code>
    /// </example>
    public DevAppInstanceDatabaseAdapterAttribute(Type adapterType)
    {
        ArgumentNullException.ThrowIfNull(adapterType);

        this.AdapterType = adapterType;
    }

    /// <summary>Gets the adapter type.</summary>
    public Type AdapterType { get; }
}
