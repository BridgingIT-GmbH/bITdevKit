// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using BridgingIT.DevKit.Presentation.Web.Auditing;

/// <summary>Checks that compact technical names retain their identifying suffixes.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~AuditDashboardTextTests</code></example>
public sealed class AuditDashboardTextTests
{
    /// <summary>Retains the complete type and method while abbreviating their namespace.</summary>
    /// <example><code>suite.ShortenKey_LongOperation_PreservesTypeAndMethod();</code></example>
    [Fact]
    public void ShortenKey_LongOperation_PreservesTypeAndMethod()
    {
        const string VALUE = "Northwind.CustomerManagement.Application.Modules.Orders.SubmitCustomerRegistrationCommand.Handle";

        var result = AuditDashboardText.ShortenKey(VALUE);

        result.Length.ShouldBeLessThanOrEqualTo(80);
        result.ShouldStartWith("Nor.Cus.App.Mod.Ord.");
        result.ShouldEndWith("SubmitCustomerRegistrationCommand.Handle");
    }

    /// <summary>Keeps the complete type with Messaging's character budget.</summary>
    /// <example><code>suite.ShortenType_LongName_PreservesType();</code></example>
    [Fact]
    public void ShortenType_LongName_PreservesType()
    {
        const string VALUE = "Northwind.CustomerManagement.Application.Modules.Orders.SubmitCustomerRegistrationCommand";

        var result = AuditDashboardText.ShortenType(VALUE);

        result.Length.ShouldBeLessThanOrEqualTo(46);
        result.ShouldEndWith("SubmitCustomerRegistrationCommand");
    }
}
