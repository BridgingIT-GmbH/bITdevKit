namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

public class MetricsTests
{
    [Fact]
    public void NormalizePart_WhenValueContainsMixedCharacters_ReturnsStableLowerCaseToken()
    {
        var result = Metrics.NormalizePart(" Signal: Order-Approved ");

        result.ShouldBe("signal_order_approved");
    }

    [Fact]
    public void NormalizeTypeName_WhenTypeIsClosedGeneric_ReturnsStableReadableToken()
    {
        var result = Metrics.NormalizeTypeName(typeof(Dictionary<string, List<int?>>));

        result.ShouldBe("dictionary_string_list_int32_nullable");
    }

    [Fact]
    public void Series_WhenPartsAreProvided_AppendsNormalizedSuffixes()
    {
        var result = Metrics.Series("orchestrations_activity_execute", "Order Approval", "Wait For Signal");

        result.ShouldBe("orchestrations_activity_execute_order_approval_wait_for_signal");
    }
}