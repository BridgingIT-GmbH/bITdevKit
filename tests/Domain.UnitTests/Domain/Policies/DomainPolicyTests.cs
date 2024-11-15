// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Policies;

[UnitTest("Domain")]
public class DomainPolicyTests
{
    [Fact]
    public async Task Apply_WithSatisfiedPolicies_ShouldSucceed()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = new IDomainPolicy<StubContext>[] { new EnabledPolicy(), new ModifyContextPolicy(), new ConditionalEnabledPolicy() };

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Messages.Count.ShouldBe(3);
        result.Messages[0].ShouldBe("Always satisfied policy applied");
        result.Messages[1].ShouldBe("Context modified");
        result.Messages[2].ShouldBe("Conditional policy applied");
        result.PolicyResults.GetValue<EnabledPolicy, int>().ShouldBe(1);
        result.PolicyResults.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(3);
        context.Value.ShouldBe(11); // Original value + 10
    }

    [Fact]
    public async Task Apply_WithNoPolicies_ShouldSucceed()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = Array.Empty<IDomainPolicy<StubContext>>();

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Messages.Count.ShouldBe(0);
        result.PolicyResults.GetValue<EnabledPolicy, int>().ShouldBe(0);
        result.PolicyResults.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(0);
        context.Value.ShouldBe(1); // Original value
    }

    [Fact]
    public async Task Apply_WithDisabledPolicy_ShouldSkipDisabledPolicy()
    {
        // Arrange
        var context = new StubContext { Value = 0 };
        var policies = new IDomainPolicy<StubContext>[] { new EnabledPolicy(), new DisabledPolicy(), new ModifyContextPolicy(), new ConditionalEnabledPolicy() };

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Messages.Count.ShouldBe(3);
        result.Messages[0].ShouldBe("Always satisfied policy applied");
        result.Messages[1].ShouldBe("Context modified");
        result.Messages[2].ShouldBe("Conditional policy applied");
        result.PolicyResults.GetValue<EnabledPolicy, int>().ShouldBe(1);
        result.PolicyResults.GetValue<DisabledPolicy, int>().ShouldBe(0);
        result.PolicyResults.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(3);
        context.Value.ShouldBe(10); // Original value + 10
    }

    [Fact]
    public async Task Apply_WithFailingPolicy_ShouldFailAndStopOnFailure()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = new IDomainPolicy<StubContext>[] { new EnabledPolicy(), new ModifyContextPolicy(), new FailingPolicy(), new ConditionalEnabledPolicy() };

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies, DomainPolicyProcessingMode.StopOnPolicyFailure);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Messages.Count.ShouldBe(2);
        result.Messages[0].ShouldBe("Always satisfied policy applied");
        result.Messages[1].ShouldBe("Context modified");
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeOfType<TestError>();
        result.PolicyResults.GetValue<EnabledPolicy, int>().ShouldBe(1);
        result.PolicyResults.GetValue<FailingPolicy, int>().ShouldBe(0);
        result.PolicyResults.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(0); // not called due to stopOnFailure
        context.Value.ShouldBe(11); // Original value + 10
    }

    [Fact]
    public async Task Apply_WithFailingPolicyAndContinueOnFailure_ShouldFailButApplyAllPolicies()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = new IDomainPolicy<StubContext>[] { new EnabledPolicy(), new ModifyContextPolicy(), new FailingPolicy(), new ConditionalEnabledPolicy() };

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies, DomainPolicyProcessingMode.ContinueOnPolicyFailure);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Messages.Count.ShouldBe(3);
        result.Messages[0].ShouldBe("Always satisfied policy applied");
        result.Messages[1].ShouldBe("Context modified");
        result.Messages[2].ShouldBe("Conditional policy applied");
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeOfType<TestError>();
        result.PolicyResults.GetValue<EnabledPolicy, int>().ShouldBe(1);
        result.PolicyResults.GetValue<FailingPolicy, int>().ShouldBe(0);
        result.PolicyResults.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(3);
        context.Value.ShouldBe(11); // Original value + 10
    }

    [Fact]
    public async Task Apply_WithFailingPolicyAndThrowOnFailure_ShouldThrowDomainPolicyException()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = new IDomainPolicy<StubContext>[]
        {
            new EnabledPolicy(), new ModifyContextPolicy(), new FailingPolicy(), // this policy will cause throw
            new ConditionalEnabledPolicy()
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<DomainPolicyException>(async () =>
        {
            await DomainPolicies.ApplyAsync(context, policies, DomainPolicyProcessingMode.ThrowOnPolicyFailure);
        });

        exception.ShouldNotBeNull();
        exception.Message.ShouldNotBeNull();
        exception.Message.ShouldBe("FailingPolicy policy failed");
        exception.Result.IsFailure.ShouldBeTrue();
        exception.Result.Messages.Count.ShouldBe(2);
        exception.Result.Messages[0].ShouldBe("Always satisfied policy applied");
        exception.Result.Messages[1].ShouldBe("Context modified");
        exception.Result.Errors.Count.ShouldBe(1);
        exception.Result.Errors.Single().ShouldBeOfType<TestError>();
        context.Value.ShouldBe(11); // Original value + 10
    }
}

public class StubContext
{
    public int Value { get; set; }
}

public class EnabledPolicy : DomainPolicyBase<StubContext>
{
    public override Task<IResult> ApplyAsync(StubContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IResult>(
            DomainPolicyResult<int>.Success(1).WithMessage("Always satisfied policy applied"));
    }
}

public class DisabledPolicy : DomainPolicyBase<StubContext>
{
    public override Task<bool> IsEnabledAsync(StubContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public override Task<IResult> ApplyAsync(StubContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IResult>(DomainPolicyResult<int>.Success(2)
            .WithMessage("This should never be called")); // TODO: looses value due to implicit conversion to Result (non generic)
    }
}

public class ConditionalEnabledPolicy : DomainPolicyBase<StubContext>
{
    public override Task<bool> IsEnabledAsync(StubContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(context.Value > 0);
    }

    public override Task<IResult> ApplyAsync(StubContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IResult>(DomainPolicyResult<int>.Success(3)
            .WithMessage("Conditional policy applied"));
    }
}

public class FailingPolicy : DomainPolicyBase<StubContext>
{
    public override Task<IResult> ApplyAsync(StubContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IResult>(DomainPolicyResult<int>.Failure()
            .WithError(new TestError()));
    }
}

public class ModifyContextPolicy : DomainPolicyBase<StubContext>
{
    public override Task<IResult> ApplyAsync(StubContext context, CancellationToken cancellationToken = default)
    {
        context.Value += 10;

        return Task.FromResult<IResult>(DomainPolicyResult<object>.Success()
            .WithMessage("Context modified"));
    }
}

public class TestError : ResultErrorBase
{
    public override string Message => "Test error occurred";
}

//public class PercentageDiscountPolicy : DomainPolicyBase<Order>
//{
//    public override Task<bool> IsSatisfiedByAsync(Order context, CancellationToken cancellationToken = default)
//        => Task.FromResult(context.TotalAmount > 0);

//    public override Task<Result> ApplyAsync(Order context, CancellationToken cancellationToken = default)
//    {
//        var discount = context.CustomerStatus switch
//        {
//            CustomerLoyaltyStatus.Regular => 0,
//            CustomerLoyaltyStatus.Silver => 0.02m,
//            CustomerLoyaltyStatus.Gold => 0.05m,
//            CustomerLoyaltyStatus.Platinum => 0.08m,
//            _ => throw new ArgumentOutOfRangeException(nameof(context.CustomerStatus))
//        };

//        return Task.FromResult<Result>(DomainPolicyResult<decimal>.Success(discount)
//            .WithMessage($"Applied percentage discount of {discount:P}"));
//    }
//}

//public class FixedAmountDiscountPolicy : DomainPolicyBase<Order>
//{
//    public override Task<bool> IsSatisfiedByAsync(Order context, CancellationToken cancellationToken = default)
//        => Task.FromResult(context.TotalAmount >= 100);

//    public override Task<Result> ApplyAsync(Order context, CancellationToken cancellationToken = default)
//    {
//        decimal discount = context.TotalAmount switch
//        {
//            >= 1000 => 50,
//            >= 500 => 25,
//            >= 100 => 10,
//            _ => 0
//        };

//        return Task.FromResult<Result>(DomainPolicyResult<decimal>.Success(discount)
//            .WithMessage($"Applied fixed discount of {discount:C}"));
//    }
//}

//public class ShippingPolicy : DomainPolicyBase<Order>
//{
//    public override Task<Result> ApplyAsync(Order context, CancellationToken cancellationToken = default)
//    {
//        var shippingMethod = context.TotalAmount switch
//        {
//            >= 1000 => "Express",
//            >= 500 => "Priority",
//            _ => "Standard"
//        };

//        return Task.FromResult<Result>(DomainPolicyResult<string>.Success(shippingMethod)
//            .WithMessage($"Selected shipping method: {shippingMethod}"));
//    }
//}

//// Usage example
//public class OrderService
//{
//    public async Task ProcessOrder(Order order)
//    {
//        var result = await DomainPolicies.ApplyAsync(order,
//            [
//                new PercentageDiscountPolicy(),
//                new FixedAmountDiscountPolicy(),
//                new ShippingPolicy()
//            ]
//        );

//        if (result.IsFailure)
//        {
//            Console.WriteLine("Order processing failed:");
//            foreach (var message in result.Messages)
//            {
//                Console.WriteLine($"- {message}");
//            }

//            return;
//        }

//        Console.WriteLine("Order processed successfully:");
//        foreach (var message in result.Messages)
//        {
//            Console.WriteLine($"- {message}");
//        }

//        var percentageDiscount = result.PolicyResults.GetValue<PercentageDiscountPolicy, decimal>();
//        var fixedDiscount = result.PolicyResults.GetValue<FixedAmountDiscountPolicy, decimal>();
//        var shippingMethod = result.PolicyResults.GetValue<ShippingPolicy, string>();

//        Console.WriteLine($"Percentage Discount: {percentageDiscount:P}");
//        Console.WriteLine($"Fixed Discount: {fixedDiscount:C}");
//        Console.WriteLine($"Shipping Method: {shippingMethod}");

//        // Additional order processing logic...
//    }
//}

//// Assumed Order and CustomerLoyaltyStatus types
//public class Order
//{
//    public decimal TotalAmount { get; set; }
//    public CustomerLoyaltyStatus CustomerStatus { get; set; }
//}

//public enum CustomerLoyaltyStatus
//{
//    Regular,
//    Silver,
//    Gold,
//    Platinum
//}