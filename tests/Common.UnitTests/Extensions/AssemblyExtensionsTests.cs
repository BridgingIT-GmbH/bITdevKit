// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using Xunit;
using NSubstitute;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Reflection;
using BridgingIT.DevKit.Common;

public class AssemblyExtensionsTests
{
    [Fact]
    public void SafeGetTypes_AssembliesIsNull_ReturnsEmpty()
    {
        // Arrange
        IEnumerable<Assembly> assemblies = null;

        // Act
        var result = assemblies.SafeGetTypes();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SafeGetTypes_AssemblyIsNull_ReturnsEmpty()
    {
        // Arrange
        Assembly assembly = null;

        // Act
        var result = assembly.SafeGetTypes();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SafeGetTypes_AssembliesInterfaceIsNull_ReturnsEmpty()
    {
        // Arrange
        IEnumerable<Assembly> assemblies = new List<Assembly> { Substitute.For<Assembly>() };
        Type @interface = null;

        // Act
        var result = assemblies.SafeGetTypes(@interface);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SafeGetTypes_AssemblyInterfaceIsNull_ReturnsEmpty()
    {
        // Arrange
        var assembly = Substitute.For<Assembly>();
        Type @interface = null;

        // Act
        var result = assembly.SafeGetTypes(@interface);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SafeGetTypes_AssembliesInterfaceIsIEnumerable_ReturnsTypesImplementingIEnumerable()
    {
        // Arrange
        IEnumerable<Assembly> assemblies = new List<Assembly> { typeof(IEnumerable<>).Assembly };
        var @interface = typeof(IEnumerable<>);

        // Act
        var result = assemblies.SafeGetTypes(@interface);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldAllBe(type => type.ImplementsInterface(@interface));
    }

    [Fact]
    public void SafeGetTypes_AssemblyInterfaceIsIEnumerable_ReturnsTypesImplementingIEnumerable()
    {
        // Arrange
        var assembly = typeof(IEnumerable<>).Assembly;
        var @interface = typeof(IEnumerable<>);

        // Act
        var result = assembly.SafeGetTypes(@interface);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldAllBe(type => type.ImplementsInterface(@interface));
    }
}