// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

[UnitTest("Common")]
public class TraceActivityDecoratorTests
{
    public TraceActivityDecoratorTests()
    {
        Sdk.CreateTracerProviderBuilder()
            .AddSource("*")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("test", serviceVersion: "1.0"))
            .Build();
    }

    [Fact]
    public void Activity_Created_For_Attribute_Marked_Method()
    {
        var service = new StubService();
        var serviceDecorated = TraceActivityDecorator<IStubService>.Create(service);

        serviceDecorated.MethodExplicitlyMarkedForTracing(() =>
        {
            Activity.Current.ShouldNotBeNull();
            this.ShouldHaveTags(Activity.Current,
                typeof(IStubService).FullName,
                "MethodExplicitlyMarkedForTracing",
                "Action");
        });
    }

    [Fact]
    public async Task Activity_Created_For_Async_Attribute_Marked_Method()
    {
        var service = new StubService();
        var serviceDecorated = TraceActivityDecorator<IStubService>.Create(service);

        await serviceDecorated.AsyncMethodExplicitlyMarkedForTracing(() =>
        {
            Activity.Current.ShouldNotBeNull();
            this.ShouldHaveTags(Activity.Current,
                typeof(IStubService).FullName,
                "AsyncMethodExplicitlyMarkedForTracing",
                "Action");
        });
    }

    [Fact]
    public void Activity_Created_MethodWithStrangeParams1()
    {
        var service = new StubService();
        var serviceDecorated = TraceActivityDecorator<IStubService>.Create(service);
        var intVal = 5;

        serviceDecorated.MethodWithStrangeParams1(() =>
            {
                Activity.Current.ShouldNotBeNull();
                this.ShouldHaveTags(Activity.Current,
                    typeof(IStubService).FullName,
                    "MethodWithStrangeParams1",
                    "Action|IList`1[]|ISet`1|IDictionary`2|Int32&");
            },
            Array.Empty<List<string>>(),
            new HashSet<int[]>(),
            new Dictionary<int, ICollection<string>>(),
            ref intVal);
    }

    [Fact]
    public void Activity_Created_MethodJaggedAndMultiDimArraysParams()
    {
        var service = new StubService();
        var serviceDecorated = TraceActivityDecorator<IStubService>.Create(service);

        serviceDecorated.MethodJaggedAndMultiDimArraysParams(() =>
            {
                Activity.Current.ShouldNotBeNull();
                this.ShouldHaveTags(Activity.Current,
                    typeof(IStubService).FullName,
                    "MethodJaggedAndMultiDimArraysParams",
                    "Action|String&|Boolean[][][]|Int16[,,][,][,,,]|Int64[][,][][,,]");
            },
            out var strVal,
            [],
            new short[,,,][,][,,] { },
            new long[,,][][,][] { });
    }

    [Fact]
    public void Activity_Not_Created_For_Non_Attribute_Marked_Method_If_All_Methods_False()
    {
        var service = new StubService();
        var serviceDecorated = TraceActivityDecorator<IStubService>.Create(service, decorateAllMethods: false);

        serviceDecorated.MethodNotExplicitlyMarkedForTracing(() => Activity.Current.ShouldBeNull());
    }

    private void ShouldHaveTags(
        Activity activity,
        string expectedClassName,
        string expectedMethodName,
        string expectedParameterTypes)
    {
        var tags = activity.Tags.ToArray();

        Array.Find(tags, t => t.Key == "code.namespace")
            .Value.ShouldBe(expectedClassName);
        Array.Find(tags, t => t.Key == "code.function")
            .Value.ShouldBe(expectedMethodName);

        if (!string.IsNullOrWhiteSpace(expectedParameterTypes))
        {
            //    Assert.Contains(tags,
            //        new KeyValuePair<string, string>("code.function.parameters", expectedParameterTypes));
        }
    }
}