namespace BridgingIT.DevKit.Domain;

public enum DomainPolicyProcessingMode
{
    ContinueOnPolicyFailure = 0,
    StopOnPolicyFailure = 1,
    ThrowOnPolicyFailure = 2
}