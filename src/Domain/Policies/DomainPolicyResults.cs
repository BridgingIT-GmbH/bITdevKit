namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;

public class DomainPolicyResults<TContext>
{
    private readonly Dictionary<Type, object> results = [];

    public void AddValue(Type policyType, object result) =>
        this.results[policyType] = result;

    public void AddValue<TPolicy>(object result)
        where TPolicy : IDomainPolicy<TContext> =>
        this.results[typeof(TPolicy)] = result;

    public TValue GetValue<TPolicy, TValue>(TValue defaultValue = default)
        where TPolicy : IDomainPolicy<TContext> =>
        this.results.TryGetValue(typeof(TPolicy), out var value) && value is TValue typedValue ? typedValue : defaultValue;

    public TValue GetValue<TValue>(Type policyType, TValue defaultValue = default) =>
        this.results.TryGetValue(policyType, out var value) && value is TValue typedValue ? typedValue : defaultValue;
}