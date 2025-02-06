# Domain Models Feature Documentation
[TOC]
## Overview

## Challenges

## Solution

## Use Cases

## Appendix B: Strongly-Typed Entity IDs

### Overview

In domain-driven design and clean architecture, entity identifiers play a crucial role. However, using primitive types like `Guid` or `int` as identifiers can lead to subtle bugs and unclear code. Consider a system managing both `Todo`s and `TodoStep`s, each using GUIDs as identifiers. A method that accidentally accepts a `TodoStep` ID when it should work with `Todo` IDs will compile successfully because both are GUIDs. 

This common anti-pattern is known as "[primitive obsession](https://wiki.c2.com/?PrimitiveObsession)" - using primitive types where a dedicated type would better express domain concepts and prevent errors.

### Solution

The TypedEntityId source generator automatically creates strongly-typed ID classes during compilation. It scans for classes marked with the `[TypedEntityId<T>]` attribute and generates corresponding ID wrapper classes that provide type safety and domain semantics.

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant CS as Compiler
    participant Gen as Generator
    participant App as Application

    Dev->>CS: Compile code with [TypedEntityId<T>]
    CS->>Gen: Trigger source generation
    Note over Gen: Find classes with TypedEntityId<T>
    Note over Gen: Extract T as underlying type
    Note over Gen: Generate ID class with:<br/>- Constructors<br/>- Value property of type T<br/>- JSON conversion<br/>- Equality methods
    Gen->>CS: Add generated code
    CS->>App: Compile final assembly
```

### Features (Generator)

The source generator creates ID classes with:

- Value wrapping and access
- Type conversions
- JSON serialization support
- Equality comparison
- Debug visualization
- Factory methods for creation
- Null handling

### Usage

#### Domain Entity
```csharp
[TypedEntityId<Guid>] // triggers the generator
public class TodoItem : Entity<TodoId> // generated id
{
    public string Title { get; set; }
    //...
}
```

#### Application Code
```csharp
// Type safety prevents mixing different ID types
public async Task<Todo> GetTodo(TodoId id) // ✅
public async Task<Todo> GetTodo(TodoStepId id) // ❌ Won't compile

// Convenient implicit conversions
TodoItemId id = Guid.NewGuid();  // Guid to TodoItemId
Guid guid = id;                  // TodoItemId to Guid
```

#### Entity Framework Configuration

The strongly-typed IDs require proper Entity Framework configuration to map between domain types and database primitives:

```csharp
public class TodoItemEntityTypeConfiguration : IEntityTypeConfiguration<Todo>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedOnAdd()
            .HasConversion(
                id => id.Value,                      // To database: TodoId -> Guid
                value => TodoItemId.Create(value));  // From database: Guid -> TodoId

        // Navigation property configuration
        builder.OwnsMany(x => x.Steps, sb =>
        {
            sb.Property(s => s.Id).ValueGeneratedOnAdd()
                .HasConversion(id => id.Value, value => TodoStepId.Create(value));
        });
    }
}
```

### Benefits

- **Type Safety**: Compiler catches ID type mismatches
- **Domain Clarity**: IDs carry semantic meaning
- **Convenience**: Implicit conversions to/from primitive types
- **Debugging**: Meaningful string representation
- **JSON Support**: Built-in serialization handling
- **Persistence**: Seamless Entity Framework integration
- **Value Semantics**: Proper equality comparison

The TypedEntityId pattern transforms primitive identifiers into first-class domain concepts, making code both safer and more expressive. It prevents a whole class of bugs while better communicating domain intent through the type system.