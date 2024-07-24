﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
#pragma warning disable RS1036 // Specify analyzer banned API enforcement setting
public class TypedEntityIdClassGenerator : ISourceGenerator
#pragma warning restore RS1036 // Specify analyzer banned API enforcement setting
{
    public void Execute(GeneratorExecutionContext context)
    {
        var compilation = context.Compilation;

        var classesWithAttribute = compilation.SyntaxTrees
            .SelectMany(st => st.GetRoot().DescendantNodes())
            .OfType<ClassDeclarationSyntax>()
            .Where(cds => cds.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().StartsWith("TypedEntityId")))
            .ToList();

        foreach (var classDeclaration in classesWithAttribute)
        {
            var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

            if (classSymbol != null)
            {
                var attribute = classSymbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass.Name.StartsWith("TypedEntityId"));
                if (attribute != null)
                {
                    if (this.ImplementsIEntity(classSymbol))
                    {
                        var underlyingType = attribute.AttributeClass.TypeArguments.First();
                        var generatedCode = GenerateIdClassCode(classSymbol, underlyingType);
                        context.AddSource($"{classSymbol.Name}Id.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "TIG001",
                                "Invalid use of TypedEntityIdAttribute",
                                "TypedEntityIdAttribute can only be applied to classes implementing IEntity (directly or indirectly)",
                                "Usage",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            classDeclaration.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        // No initialization required
    }

    private bool ImplementsIEntity(INamedTypeSymbol classSymbol)
    {
        return classSymbol.AllInterfaces.Any(i => i.Name == "IEntity");
    }

#pragma warning disable SA1204 // Static elements should appear before instance elements
    private static string GenerateIdClassCode(INamedTypeSymbol entityType, ITypeSymbol underlyingType)
#pragma warning restore SA1204 // Static elements should appear before instance elements
    {
        var className = $"{entityType.Name}Id";
        var namespaceName = entityType.ContainingNamespace.ToDisplayString();
        var typeName = underlyingType.Name;

        var createMethod = GetCreateMethod(underlyingType, className);
        var parseMethod = GetParseMethod(underlyingType, className);

        return $@"
// <auto-generated />
namespace {namespaceName}
{{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using BridgingIT.DevKit.Domain.Model;

    [DebuggerDisplay(""{{Value}}"")]
    public class {className} : EntityId<{typeName}>
    {{
        private {className}()
        {{
        }}

        private {className}({typeName} value)
        {{
            this.Value = value;
        }}

        public override {typeName} Value {{ get; protected set; }}

        public bool IsEmpty => this.Value{GetIsEmptyCheck(underlyingType)};

        public static implicit operator {typeName}({className} id) => id?.Value ?? default; // allows a TypedId value to be implicitly converted to a type value.
        public static implicit operator string({className} id) => id?.Value.ToString(); // allows a TypedId value to be implicitly converted to a string.
        public static implicit operator {className}({typeName} id) => Create(id); // allows a type value to be implicitly converted to a TypedId object.
        public static implicit operator {className}(string id) => Create(id); // allows a string value to be implicitly converted to a TypedId object.

        {createMethod}

        public static {className} Create({typeName} id)
        {{
            return new {className}(id);
        }}

        {parseMethod}

        protected override IEnumerable<object> GetAtomicValues()
        {{
            yield return this.Value;
        }}
    }}
}}";
    }

    private static string GetCreateMethod(ITypeSymbol underlyingType, string className)
    {
        if (underlyingType.ToString() == "System.Guid")
        {
            return $@"public static {className} Create()
        {{
            return new {className}(Guid.NewGuid());
        }}";
        }
        else if (underlyingType.SpecialType == SpecialType.System_Int32 || underlyingType.SpecialType == SpecialType.System_Int64)
        {
            return $@"// Note: Implement a strategy for generating new IDs for integer/long
        public static {className} Create()
        {{
            //Implement a strategy for generating new integer IDs
            throw new NotImplementedException();
        }}";
        }
        else
        {
            return $@"// Note: Implement a strategy for generating new IDs for this type
        public static {className} Create()
        {{
            //Implement a strategy for generating new integer IDs
            throw new NotImplementedException();
        }}";
        }
    }

    private static string GetParseMethod(ITypeSymbol underlyingType, string className)
    {
        if (underlyingType.ToString() == "System.Guid")
        {
            return $@"public static {className} Create(string id)
        {{
            if (string.IsNullOrEmpty(id))
            {{
                throw new ArgumentException(""Id cannot be null or empty."", nameof(id));
            }}

            return new {className}(Guid.Parse(id));
        }}";
        }
        else if (underlyingType.SpecialType == SpecialType.System_Int32)
        {
            return $@"public static {className} Create(string id)
        {{
            if (string.IsNullOrEmpty(id))
            {{
                throw new ArgumentException(""Id cannot be null or empty."", nameof(id));
            }}

            return new {className}(int.Parse(id));
        }}";
        }
        else if (underlyingType.SpecialType == SpecialType.System_Int64)
        {
            return $@"public static {className} Create(string id)
        {{
            if (string.IsNullOrEmpty(id))
            {{
                throw new ArgumentException(""Id cannot be null or empty."", nameof(id));
            }}

            return new {className}(long.Parse(id));
        }}";
        }
        else if (underlyingType.SpecialType == SpecialType.System_String)
        {
            return $@"public static {className} Create(string id)
        {{
            if (string.IsNullOrEmpty(id))
            {{
                throw new ArgumentException(""Id cannot be null or empty."", nameof(id));
            }}

            return new {className}(id);
        }}";
        }
        else
        {
            return $@"// Note: Implement a custom parsing strategy for this type
        public static {className} Create(string id)
        {{
            throw new NotImplementedException(""Implement a custom parsing strategy for this type"");
        }}";
        }
    }

    private static string GetIsEmptyCheck(ITypeSymbol underlyingType)
    {
        if (underlyingType.ToString() == "System.Guid")
        {
            return " == Guid.Empty";
        }
        else if (underlyingType.SpecialType == SpecialType.System_Int32 || underlyingType.SpecialType == SpecialType.System_Int64)
        {
            return " == 0";
        }
        else if (underlyingType.SpecialType == SpecialType.System_String)
        {
            return " == null || this.Value == string.Empty";
        }
        else
        {
            return "; // Implement custom empty check for this type";
        }
    }
}