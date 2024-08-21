namespace BridgingIT.DevKit.Domain;

using System.Collections.Generic;
using BridgingIT.DevKit.Common;

public class DomainPolicyResult<TValue> : Result<TValue>
{
    public DomainPolicyResults<TValue> PolicyResults { get; set; } = new DomainPolicyResults<TValue>();

    public static new DomainPolicyResult<TValue> Success(TValue value) => new() { Value = value };

    public static new DomainPolicyResult<TValue> Success() => new();

    public static new DomainPolicyResult<TValue> Failure() => new() { IsSuccess = false };

    public new DomainPolicyResult<TValue> WithMessage(string message)
    {
        base.WithMessage(message);
        return this;
    }

    public new DomainPolicyResult<TValue> WithMessages(IEnumerable<string> messages)
    {
        base.WithMessages(messages);
        return this;
    }

    public new DomainPolicyResult<TValue> WithError(IResultError error)
    {
        base.WithError(error);
        return this;
    }

    public new DomainPolicyResult<TValue> WithErrors(IEnumerable<IResultError> errors)
    {
        base.WithErrors(errors);
        return this;
    }

    public DomainPolicyResult<TValue> WithPolicyResults(DomainPolicyResults<TValue> results)
    {
        this.PolicyResults = results;
        return this;
    }
}
