// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Represents a container for storing and managing policy results related to a specific domain context.
///     This class allows adding, retrieving, and managing policy results by their type.
/// </summary>
/// <typeparam name="TContext">The type of the context associated with the domain policies.</typeparam>
public class DomainPolicyResults<TContext> // TODO: use struct here? as in the result class
{
    /// <summary>
    ///     Stores the results of domain policies against various policy types.
    /// </summary>
    private readonly Dictionary<Type, object> results = [];

    /// <summary>
    ///     Adds a result value to the dictionary of policy results.
    /// </summary>
    /// <param name="policyType">The type of the policy for which the result is being added.</param>
    /// <param name="result">The result value to be associated with the specified policy type.</param>
    public void AddValue(Type policyType, object result)
    {
        this.results[policyType] = result;
    }

    /// <summary>
    ///     Adds the specified result to the policy results dictionary using the policy type as the key.
    /// </summary>
    /// <typeparam name="TPolicy">The type of the policy to which the result is related.</typeparam>
    /// <param name="result">The result object to be associated with the specified policy type.</param>
    public void AddValue<TPolicy>(object result)
        where TPolicy : IDomainPolicy<TContext>
    {
        this.results[typeof(TPolicy)] = result;
    }

    /// <summary>
    ///     Retrieves the value associated with the given policy type.
    /// </summary>
    /// <typeparam name="TPolicy">The policy type to look up the value for.</typeparam>
    /// <typeparam name="TValue">The expected type of the value.</typeparam>
    /// <param name="defaultValue">
    ///     The default value to return if the policy type is not found or if the value has an
    ///     incompatible type.
    /// </param>
    /// <returns>
    ///     The value associated with the specified policy type if found and of the correct type, otherwise the default
    ///     value.
    /// </returns>
    public TValue GetValue<TPolicy, TValue>(TValue defaultValue = default)
        where TPolicy : IDomainPolicy<TContext>
    {
        return this.results.TryGetValue(typeof(TPolicy), out var value) && value is TValue typedValue
            ? typedValue
            : defaultValue;
    }

    /// <summary>
    ///     Retrieves the stored value based on the specified policy type. If the value does not exist or is not of the
    ///     expected type,
    ///     the provided default value is returned.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to be retrieved.</typeparam>
    /// <param name="policyType">The type of the policy used to identify the stored value.</param>
    /// <param name="defaultValue">
    ///     The default value to return if the stored value does not exist or is not of the expected
    ///     type.
    /// </param>
    /// <returns>
    ///     Returns the stored value if it exists and is of the expected type, otherwise returns the provided default
    ///     value.
    /// </returns>
    public TValue GetValue<TValue>(Type policyType, TValue defaultValue = default)
    {
        return this.results.TryGetValue(policyType, out var value) && value is TValue typedValue
            ? typedValue
            : defaultValue;
    }
}