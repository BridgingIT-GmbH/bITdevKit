// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class TypeExtensionsTests
{
    [Fact]
    public void IsOfType_NullSource_ReturnsFalse()
    {
        // Arrange
        object source = null;
        var targetType = typeof(string);

        // Act
        var result = source.IsOfType(targetType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsOfType_SourceIsOfType_ReturnsTrue()
    {
        // Arrange
        const string source = "Hello";
        var targetType = typeof(string);

        // Act
        var result = source.IsOfType(targetType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsOfType_SourceIsNotOfType_ReturnsFalse()
    {
        // Arrange
        const int source = 42;
        var targetType = typeof(string);

        // Act
        var result = source.IsOfType(targetType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNotOfType_NullSource_ReturnsFalse()
    {
        // Arrange
        object source = null;
        var targetType = typeof(string);

        // Act
        var result = source.IsNotOfType(targetType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNotOfType_SourceIsOfType_ReturnsFalse()
    {
        // Arrange
        const string source = "Hello";
        var targetType = typeof(string);

        // Act
        var result = source.IsNotOfType(targetType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNotOfType_SourceIsNotOfType_ReturnsTrue()
    {
        // Arrange
        const int source = 42;
        var targetType = typeof(string);

        // Act
        var result = source.IsNotOfType(targetType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void PrettyName_NullSource_ReturnsEmptyString()
    {
        // Arrange
        Type source = null;

        // Act
        var result = source.PrettyName();

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void PrettyName_NonGenericType_ReturnsTypeName()
    {
        // Arrange
        var source = typeof(int);

        // Act
        var result = source.PrettyName();

        // Assert
        result.ShouldBe("Int32");
    }

    [Fact]
    public void PrettyName_GenericTypeWithAngleBrackets_ReturnsPrettyName()
    {
        // Arrange
        var source = typeof(Dictionary<int, string>);

        // Act
        var result = source.PrettyName();

        // Assert
        result.ShouldBe("Dictionary<Int32,String>");
    }

    [Fact]
    public void PrettyName_GenericTypeWithoutAngleBrackets_ReturnsPrettyName()
    {
        // Arrange
        var source = typeof(Dictionary<int, string>);

        // Act
        var result = source.PrettyName(false);

        // Assert
        result.ShouldBe("Dictionary[Int32,String]");
    }

    [Fact]
    public void FullPrettyName_NullSource_ReturnsEmptyString()
    {
        // Arrange
        Type source = null;

        // Act
        var result = source.FullPrettyName();

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void FullPrettyName_NonGenericType_ReturnsFullTypeName()
    {
        // Arrange
        var source = typeof(int);

        // Act
        var result = source.FullPrettyName();

        // Assert
        result.ShouldBe("System.Int32");
    }

    [Fact]
    public void FullPrettyName_GenericTypeWithAngleBrackets_ReturnsFullPrettyName()
    {
        // Arrange
        var source = typeof(Dictionary<int, string>);

        // Act
        var result = source.FullPrettyName();

        // Assert
        result.ShouldBe("System.Collections.Generic.Dictionary<System.Int32,System.String>");
    }

    [Fact]
    public void FullPrettyName_GenericTypeWithoutAngleBrackets_ReturnsFullPrettyName()
    {
        // Arrange
        var source = typeof(Dictionary<int, string>);

        // Act
        var result = source.FullPrettyName(false);

        // Assert
        result.ShouldBe("System.Collections.Generic.Dictionary[System.Int32,System.String]");
    }

    [Fact]
    public void AssemblyQualifiedNameShort_ReturnsShortAssemblyQualifiedName()
    {
        // Arrange
        var source = typeof(int);

        // Act
        var result = source.AssemblyQualifiedNameShort();

        // Assert
        result.ShouldBe("System.Int32, System.Private.CoreLib");
    }

    [Fact]
    public void IsNumeric_ArrayType_ReturnsFalse()
    {
        // Arrange
        var type = typeof(int[]);

        // Act
        var result = type.IsNumeric();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNumeric_NumericType_ReturnsTrue()
    {
        // Arrange
        var numericTypes = new[]
        {
            typeof(byte), typeof(decimal), typeof(double), typeof(short), typeof(int), typeof(long), typeof(sbyte), typeof(float), typeof(ushort), typeof(uint), typeof(ulong)
        };

        foreach (var type in numericTypes)
        {
            // Act
            var result = type.IsNumeric();

            // Assert
            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void IsNumeric_NonNumericType_ReturnsFalse()
    {
        // Arrange
        var nonNumericTypes = new[] { typeof(object), typeof(string), typeof(DateTime), typeof(Guid) };

        foreach (var type in nonNumericTypes)
        {
            // Act
            var result = type.IsNumeric();

            // Assert
            result.ShouldBeFalse();
        }
    }

    [Fact]
    public void GetFieldUnambiguous_NullSource_ThrowsArgumentNullException()
    {
        // Arrange
        Type source = null;
        const string name = "fieldName";

        // Act and Assert
        Should.Throw<ArgumentNullException>(() => source.GetFieldUnambiguous(name));
    }

    [Fact]
    public void GetFieldUnambiguous_NullName_ThrowsArgumentNullException()
    {
        // Arrange
        var source = typeof(MyClass);
        const string name = null;

        // Act and Assert
        Should.Throw<ArgumentNullException>(() => source.GetFieldUnambiguous(name));
    }

    [Fact]
    public void GetFieldUnambiguous_FieldExistsInSourceType_ReturnsFieldInfo()
    {
        // Arrange
        var source = typeof(MyClass);
        const string name = "Field1";

        // Act
        var result = source.GetFieldUnambiguous(name);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(name);
        result.FieldType.ShouldBe(typeof(int));
    }

    [Fact]
    public void GetFieldUnambiguous_FieldExistsInBaseType_ReturnsFieldInfo()
    {
        // Arrange
        var source = typeof(MyDerivedClass);
        const string name = "Field1";

        // Act
        var result = source.GetFieldUnambiguous(name);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(name);
        result.FieldType.ShouldBe(typeof(int));
    }

    [Fact]
    public void GetFieldUnambiguous_FieldDoesNotExist_ReturnsNull()
    {
        // Arrange
        var source = typeof(MyClass);
        const string name = "nonExistentField";

        // Act
        var result = source.GetFieldUnambiguous(name);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetPropertyUnambiguous_NullSource_ThrowsArgumentNullException()
    {
        // Arrange
        Type source = null;
        const string name = "propertyName";

        // Act and Assert
        Should.Throw<ArgumentNullException>(() => source.GetPropertyUnambiguous(name));
    }

    [Fact]
    public void GetPropertyUnambiguous_NullName_ThrowsArgumentNullException()
    {
        // Arrange
        var source = typeof(MyClass);
        const string name = null;

        // Act and Assert
        Should.Throw<ArgumentNullException>(() => source.GetPropertyUnambiguous(name));
    }

    [Fact]
    public void GetPropertyUnambiguous_PropertyExistsInSourceType_ReturnsPropertyInfo()
    {
        // Arrange
        var source = typeof(MyClass);
        const string name = "Property1";

        // Act
        var result = source.GetPropertyUnambiguous(name);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(name);
        result.PropertyType.ShouldBe(typeof(string));
    }

    [Fact]
    public void GetPropertyUnambiguous_PropertyExistsInBaseType_ReturnsPropertyInfo()
    {
        // Arrange
        var source = typeof(MyDerivedClass);
        const string name = "Property1";

        // Act
        var result = source.GetPropertyUnambiguous(name);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(name);
        result.PropertyType.ShouldBe(typeof(string));
    }

    [Fact]
    public void GetPropertyUnambiguous_PropertyDoesNotExist_ReturnsNull()
    {
        // Arrange
        var source = typeof(MyClass);
        const string name = "NonExistentProperty";

        // Act
        var result = source.GetPropertyUnambiguous(name);

        // Assert
        result.ShouldBeNull();
    }

    private class MyClass
    {
#pragma warning disable CS0649 // Field 'TypeExtensionsTests.MyClass.Field1' is never assigned to, and will always have its default value 0
        public int Field1;
#pragma warning restore CS0649 // Field 'TypeExtensionsTests.MyClass.Field1' is never assigned to, and will always have its default value 0

        public string Property1 { get; set; }
    }

    private class MyDerivedClass : MyClass { }
}