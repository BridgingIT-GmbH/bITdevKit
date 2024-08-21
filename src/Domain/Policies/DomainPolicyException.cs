namespace BridgingIT.DevKit.Domain;

using System;
using BridgingIT.DevKit.Common;

public class DomainPolicyException : Exception
{
    public DomainPolicyException()
        : base()
    {
    }

    public DomainPolicyException(string message)
        : base(message)
    {
    }

    public DomainPolicyException(string message, Result result)
        : base(message)
    {
        this.Result = result;
    }

    public DomainPolicyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Result Result { get; }

    public override string ToString()
    {
        var result = this.Message;

        if (this.Result.Errors.SafeAny())
        {
            if (!result.IsNullOrEmpty())
            {
                result += Environment.NewLine;
            }

            result += "Errors: ";
            result += this.Result.Errors.ToString(", ");
        }

        if (this.Result.Messages.SafeAny())
        {
            if (!result.IsNullOrEmpty())
            {
                result += Environment.NewLine;
            }

            result += "Messages: ";
            result += this.Result.Messages.ToString(", ");
        }

        return result;
    }
}