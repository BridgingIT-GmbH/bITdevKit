// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a value that can be of two different types. This class provides a type-safe way to handle scenarios
/// where a value can be one of two distinct types.
/// </summary>
/// <typeparam name="T1">The type of the first possible value.</typeparam>
/// <typeparam name="T2">The type of the second possible value.</typeparam>
/// <example>
/// <code>
/// // Basic usage examples
/// var first = new Either&lt;int, string&gt;(42);
/// var second = new Either&lt;int, string&gt;("Hello");
///
/// // Using implicit operators
/// Either&lt;int, string&gt; fromInt = 42;
/// Either&lt;int, string&gt; fromString = "Hello";
///
/// // Pattern matching
/// var result = first.Match(
///     num => $"Number is {num}",
///     str => $"String is {str}"
/// );
/// </code>
/// </example>
public class Either<T1, T2>
{
    protected int Index { get; init; } = 0;

    protected T1 Value1 { get; init; }

    protected T2 Value2 { get; init; }

    protected Either()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Either{T1,T2}"/> class with a first value.
    /// </summary>
    /// <param name="value">The first value to store.</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    /// <example>
    /// <code>
    /// // Basic construction
    /// var either = new Either&lt;int, string&gt;(42);
    /// Console.WriteLine(either.IsFirst); // True
    ///
    /// // Error handling example
    /// try
    /// {
    ///     var invalidEither = new Either&lt;string, int&gt;(null);
    /// }
    /// catch (ArgumentNullException ex)
    /// {
    ///     Console.WriteLine("Cannot create Either with null value");
    /// }
    ///
    /// // Usage in collections
    /// var list = new List&lt;Either&lt;int, string&gt;&gt;
    /// {
    ///     new(42),
    ///     new("Hello")
    /// };
    /// </code>
    /// </example>
    public Either(T1 value)
    {
        ArgumentNullException.ThrowIfNull(value);

        this.Value1 = value;
        this.Index = 1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Either{T1,T2}"/> class with a second value.
    /// </summary>
    /// <param name="value">The second value to store.</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    /// <example>
    /// <code>
    /// // Basic construction
    /// var either = new Either&lt;int, string&gt;("Hello");
    /// Console.WriteLine(either.IsSecond); // True
    ///
    /// // Error handling example
    /// try
    /// {
    ///     var invalidEither = new Either&lt;int, string&gt;(null);
    /// }
    /// catch (ArgumentNullException ex)
    /// {
    ///     Console.WriteLine("Cannot create Either with null value");
    /// }
    ///
    /// // Usage with method parameters
    /// void ProcessValue(Either&lt;int, string&gt; value)
    /// {
    ///     value.Switch(
    ///         num => Console.WriteLine($"Got number: {num}"),
    ///         str => Console.WriteLine($"Got string: {str}")
    ///     );
    /// }
    ///
    /// ProcessValue(new Either&lt;int, string&gt;("Test"));
    /// </code>
    /// </example>
    public Either(T2 value)
    {
        ArgumentNullException.ThrowIfNull(value);

        this.Value2 = value;
        this.Index = 2;
    }

    /// <summary>
    /// Gets the first value if it exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the instance contains the second value.</exception>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Safe access pattern
    /// if (either.IsFirst)
    /// {
    ///     int value = either.FirstValue;
    ///     Console.WriteLine($"Got number: {value}");
    /// }
    ///
    /// // Error handling example
    /// try
    /// {
    ///     var stringEither = new Either&lt;int, string&gt;("Hello");
    ///     int invalidAccess = stringEither.FirstValue; // Will throw
    /// }
    /// catch (InvalidOperationException ex)
    /// {
    ///     Console.WriteLine("Attempted to access FirstValue when containing second type");
    /// }
    ///
    /// // Pattern with null coalescing
    /// int number = either.IsFirst ? either.FirstValue : -1;
    /// </code>
    /// </example>
    public T1 FirstValue => this.Index == 1
        ? this.Value1
        : throw new InvalidOperationException("Either contains second value");

    /// <summary>
    /// Gets the second value if it exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the instance contains the first value.</exception>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;("Hello");
    ///
    /// // Safe access pattern
    /// if (either.IsSecond)
    /// {
    ///     string value = either.SecondValue;
    ///     Console.WriteLine($"Got string: {value}");
    /// }
    ///
    /// // Error handling example
    /// try
    /// {
    ///     var intEither = new Either&lt;int, string&gt;(42);
    ///     string invalidAccess = intEither.SecondValue; // Will throw
    /// }
    /// catch (InvalidOperationException ex)
    /// {
    ///     Console.WriteLine("Attempted to access SecondValue when containing first type");
    /// }
    ///
    /// // LINQ usage example
    /// var items = new List&lt;Either&lt;int, string&gt;&gt;();
    /// var strings = items.Where(e => e.IsSecond).Select(e => e.SecondValue);
    /// </code>
    /// </example>
    public T2 SecondValue => this.Index == 2
        ? this.Value2
        : throw new InvalidOperationException("Either contains first value");

    /// <summary>
    /// Retrieves the value stored in the <see cref="Either{T1,T2}"/> instance, converting it to the specified type.
    /// </summary>
    /// <typeparam name="TOutput">The type to which the stored value will be cast.</typeparam>
    /// <returns>The stored value cast to the specified type <typeparamref name="TOutput"/>.</returns>
    /// <exception cref="InvalidCastException">Thrown when the stored value cannot be cast to the specified type.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the Either instance does not contain a value.</exception>
    public TOutput GetValue<TOutput>()
    {
        return this.Index switch
        {
            1 => (TOutput)(object)this.Value1,
            2 => (TOutput)(object)this.Value2,
            _ => default
        };
    }

    /// <summary>
    /// Attempts to retrieve the first value stored in the <see cref="Either{T1,T2}"/> instance.
    /// </summary>
    /// <param name="first">When this method returns, contains the first value if the retrieval succeeded, or the default value if it failed.</param>
    /// <returns>true if the first value is successfully retrieved; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the <see cref="Either{T1,T2}"/> instance contains the second value.</exception>
    public bool TryGetFirstValue(out T1 first)
    {
        if (this.Index == 1)
        {
            first = this.FirstValue;
            return true;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Attempts to retrieve the second value stored in the instance.
    /// </summary>
    /// <param name="second">The variable to store the second value if it exists.</param>
    /// <returns>True if the instance contains the second value; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the instance contains the first value.</exception>
    public bool TryGetSecondValue(out T2 second)
    {
        if (this.Index == 2)
        {
            second = this.SecondValue;
            return true;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Gets a value indicating whether the instance contains the first value type.
    /// </summary>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Basic check
    /// if (either.IsFirst)
    /// {
    ///     Console.WriteLine($"Number value: {either.FirstValue}");
    /// }
    ///
    /// // Combined with switch pattern
    /// either.Switch(
    ///     firstAction: num => Console.WriteLine($"Got number: {num}"),
    ///     secondAction: _ => Console.WriteLine("Not a number")
    /// );
    ///
    /// // Use in LINQ queries
    /// var numbers = items
    ///     .Where(e => e.IsFirst)
    ///     .Select(e => e.FirstValue);
    /// </code>
    /// </example>
    public bool IsFirst => this.Index == 1;

    /// <summary>
    /// Gets a value indicating whether the instance contains the second value type.
    /// </summary>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;("Hello");
    ///
    /// // Basic check
    /// if (either.IsSecond)
    /// {
    ///     Console.WriteLine($"String value: {either.SecondValue}");
    /// }
    ///
    /// // Validation example
    /// public bool ValidateEither(Either&lt;int, string&gt; input)
    /// {
    ///     if (input.IsSecond)
    ///     {
    ///         return !string.IsNullOrEmpty(input.SecondValue);
    ///     }
    ///     return input.FirstValue > 0;
    /// }
    ///
    /// // Collection processing
    /// var errorMessages = items
    ///     .Where(e => e.IsSecond)
    ///     .Select(e => e.SecondValue)
    ///     .ToList();
    /// </code>
    /// </example>
    public bool IsSecond => this.Index == 2;

    /// <summary>
    /// Implicitly converts a value of the first type to an Either instance.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A new Either instance containing the first value.</returns>
    /// <example>
    /// <code>
    /// // Direct assignment
    /// Either&lt;int, string&gt; either = 42;
    ///
    /// // Method parameter
    /// void ProcessEither(Either&lt;int, string&gt; value) { }
    /// ProcessEither(42); // Implicit conversion
    ///
    /// // In collections
    /// var list = new List&lt;Either&lt;int, string&gt;&gt;
    /// {
    ///     42,                // Implicit conversion
    ///     "Hello"           // Implicit conversion
    /// };
    /// </code>
    /// </example>
    public static implicit operator Either<T1, T2>(T1 value) => new(value);

    /// <summary>
    /// Implicitly converts a value of the second type to an Either instance.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A new Either instance containing the second value.</returns>
    /// <example>
    /// <code>
    /// // Direct assignment
    /// Either&lt;int, string&gt; either = "Hello";
    ///
    /// // In expression
    /// Either&lt;int, string&gt; GetValue(bool useNumber)
    ///     => useNumber ? 42 : "Not a number";
    ///
    /// // With LINQ
    /// var results = items.Select&lt;string, Either&lt;int, string&gt;&gt;(item =>
    ///     int.TryParse(item, out var number) ? number : item);
    /// </code>
    /// </example>
    public static implicit operator Either<T1, T2>(T2 value) => new(value);

    /// <summary>
    /// Creates an Either instance from the first value type.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <returns>A new Either instance containing the first value.</returns>
    /// <example>
    /// <code>
    /// // Basic factory usage
    /// var either = Either&lt;int, string&gt;.FromFirst(42);
    ///
    /// // Useful when type inference doesn't work
    /// var items = new List&lt;Either&lt;int, string&gt;&gt;
    /// {
    ///     Either&lt;int, string&gt;.FromFirst(42),
    ///     Either&lt;int, string&gt;.FromSecond("Hello")
    /// };
    ///
    /// // With conditional logic
    /// var result = condition
    ///     ? Either&lt;int, string&gt;.FromFirst(42)
    ///     : Either&lt;int, string&gt;.FromSecond("Error");
    /// </code>
    /// </example>
    public static Either<T1, T2> FromFirst(T1 value) => new(value);

    /// <summary>
    /// Creates an Either instance from the second value type.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <returns>A new Either instance containing the second value.</returns>
    /// <example>
    /// <code>
    /// // Basic factory usage
    /// var either = Either&lt;int, string&gt;.FromSecond("Hello");
    ///
    /// // Error handling pattern
    /// public Either&lt;int, string&gt; ParseNumber(string input)
    /// {
    ///     if (int.TryParse(input, out var number))
    ///     {
    ///         return Either&lt;int, string&gt;.FromFirst(number);
    ///     }
    ///     return Either&lt;int, string&gt;.FromSecond("Invalid number format");
    /// }
    ///
    /// // LINQ usage
    /// var results = items.Select(item =>
    ///     item.IsValid
    ///         ? Either&lt;int, string&gt;.FromFirst(item.Value)
    ///         : Either&lt;int, string&gt;.FromSecond(item.ErrorMessage)
    /// );
    /// </code>
    /// </example>
    public static Either<T1, T2> FromSecond(T2 value) => new(value);

    /// <summary>
    /// Matches the contained value with the appropriate function and returns the result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="firstMatch">The function to execute if the instance contains the first type.</param>
    /// <param name="secondMatch">The function to execute if the instance contains the second type.</param>
    /// <returns>The result of the matching function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either match function is null.</exception>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Simple string conversion
    /// string result = either.Match(
    ///     firstMatch: num => $"Number: {num}",
    ///     secondMatch: str => $"Text: {str}"
    /// );
    ///
    /// // Object transformation
    /// var dto = either.Match(
    ///     firstMatch: num => new SuccessDto { Value = num },
    ///     secondMatch: err => new ErrorDto { Message = err }
    /// );
    ///
    /// // Validation example
    /// bool isValid = either.Match(
    ///     firstMatch: num => num > 0,
    ///     secondMatch: str => !string.IsNullOrEmpty(str)
    /// );
    ///
    /// // Error handling
    /// try
    /// {
    ///     var result = either.Match(null, str => str);
    /// }
    /// catch (ArgumentNullException ex)
    /// {
    ///     Console.WriteLine("Match functions cannot be null");
    /// }
    /// </code>
    /// </example>
    public T Match<T>(Func<T1, T> firstMatch, Func<T2, T> secondMatch)
    {
        ArgumentNullException.ThrowIfNull(firstMatch);
        ArgumentNullException.ThrowIfNull(secondMatch);

        return this.Index == 1 ? firstMatch(this.Value1) : secondMatch(this.Value2);
    }

    /// <summary>
    /// Executes the appropriate action based on the contained value type.
    /// </summary>
    /// <param name="firstAction">The action to execute if the instance contains the first type.</param>
    /// <param name="secondAction">The action to execute if the instance contains the second type.</param>
    /// <exception cref="ArgumentNullException">Thrown when either action is null.</exception>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Basic usage
    /// either.Switch(
    ///     firstAction: num => Console.WriteLine($"Number: {num}"),
    ///     secondAction: str => Console.WriteLine($"Text: {str}")
    /// );
    ///
    /// // With logging
    /// either.Switch(
    ///     firstAction: num =>
    ///     {
    ///         logger.LogInformation("Processing number: {Number}", num);
    ///         ProcessNumber(num);
    ///     },
    ///     secondAction: str =>
    ///     {
    ///         logger.LogInformation("Processing text: {Text}", str);
    ///         ProcessText(str);
    ///     }
    /// );
    ///
    /// // Error handling with validation
    /// either.Switch(
    ///     firstAction: num =>
    ///     {
    ///         if (num &lt; 0) throw new ArgumentException("Number must be positive");
    ///         ProcessPositiveNumber(num);
    ///     },
    ///     secondAction: str =>
    ///     {
    ///         if (string.IsNullOrEmpty(str)) throw new ArgumentException("String cannot be empty");
    ///         ProcessNonEmptyString(str);
    ///     }
    /// );
    /// </code>
    /// </example>
    public void Switch(Action<T1> firstAction, Action<T2> secondAction)
    {
        ArgumentNullException.ThrowIfNull(firstAction);
        ArgumentNullException.ThrowIfNull(secondAction);

        if (this.Index == 1)
        {
            firstAction(this.Value1);
        }
        else
        {
            secondAction(this.Value2);
        }
    }

    /// <summary>
    /// Asynchronously matches the contained value with the appropriate function and returns the result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="firstMatch">The async function to execute if the instance contains the first type.</param>
    /// <param name="secondMatch">The async function to execute if the instance contains the second type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either match function is null.</exception>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Basic async transformation
    /// var result = await either.MatchAsync(
    ///     firstMatch: async num => await GetUserByIdAsync(num),
    ///     secondMatch: async str => await GetUserByNameAsync(str)
    /// );
    ///
    /// // With error handling
    /// try
    /// {
    ///     var result = await either.MatchAsync(
    ///         firstMatch: async num =>
    ///         {
    ///             var user = await dbContext.Users.FindAsync(num);
    ///             if (user == null) throw new KeyNotFoundException();
    ///             return user;
    ///         },
    ///         secondMatch: async str =>
    ///         {
    ///             var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == str);
    ///             if (user == null) throw new KeyNotFoundException();
    ///             return user;
    ///         }
    ///     );
    /// }
    /// catch (KeyNotFoundException)
    /// {
    ///     // Handle not found
    /// }
    ///
    /// // With cancellation
    /// using var cts = new CancellationTokenSource();
    /// var result = await either.MatchAsync(
    ///     async num => await ProcessNumberAsync(num, cts.Token),
    ///     async str => await ProcessStringAsync(str, cts.Token)
    /// );
    /// </code>
    /// </example>
    public async Task<T> MatchAsync<T>(Func<T1, Task<T>> firstMatch, Func<T2, Task<T>> secondMatch)
    {
        ArgumentNullException.ThrowIfNull(firstMatch);
        ArgumentNullException.ThrowIfNull(secondMatch);

        return this.Index == 1
            ? await firstMatch(this.Value1)
            : await secondMatch(this.Value2);
    }

    /// <summary>
    /// Asynchronously executes the appropriate action based on the contained value type.
    /// </summary>
    /// <param name="firstAction">The async action to execute if the instance contains the first type.</param>
    /// <param name="secondAction">The async action to execute if the instance contains the second type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either action is null.</exception>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Basic async operation
    /// await either.SwitchAsync(
    ///     firstAction: async num => await SaveNumberAsync(num),
    ///     secondAction: async str => await SaveStringAsync(str)
    /// );
    ///
    /// // With dependency injection and logging
    /// await either.SwitchAsync(
    ///     firstAction: async num =>
    ///     {
    ///         await logger.LogInformationAsync("Processing number: {Number}", num);
    ///         await dataService.ProcessNumberAsync(num);
    ///     },
    ///     secondAction: async str =>
    ///     {
    ///         await logger.LogInformationAsync("Processing text: {Text}", str);
    ///         await dataService.ProcessTextAsync(str);
    ///     }
    /// );
    ///
    /// // With retry policy
    /// await either.SwitchAsync(
    ///     firstAction: async num =>
    ///     {
    ///         await Policy
    ///             .Handle&lt;HttpRequestException&gt;()
    ///             .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)))
    ///             .ExecuteAsync(async () => await ProcessNumberAsync(num));
    ///     },
    ///     secondAction: async str =>
    ///     {
    ///         await Policy
    ///             .Handle&lt;HttpRequestException&gt;()
    ///             .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)))
    ///             .ExecuteAsync(async () => await ProcessStringAsync(str));
    ///     }
    /// );
    /// </code>
    /// </example>
    public async Task SwitchAsync(Func<T1, Task> firstAction, Func<T2, Task> secondAction)
    {
        ArgumentNullException.ThrowIfNull(firstAction);
        ArgumentNullException.ThrowIfNull(secondAction);

        if (this.Index == 1)
        {
            await firstAction(this.Value1);
        }
        else
        {
            await secondAction(this.Value2);
        }
    }

    /// <summary>
    /// Converts the Either instance to a string representation based on its contained value.
    /// </summary>
    /// <returns>A string representation of the contained value.</returns>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    /// Console.WriteLine(either.ToString()); // "First(42)"
    ///
    /// var stringEither = new Either&lt;int, string&gt;("Hello");
    /// Console.WriteLine(stringEither.ToString()); // "Second(Hello)"
    /// </code>
    /// </example>
    public override string ToString()
    {
        return this.Match(
            firstMatch: value => $"{typeof(T1).Name}: {value}",
            secondMatch: value => $"{typeof(T2).Name}: {value}");
    }

    /// <summary>
    /// Creates an Either instance from a nullable value, using the second type for the null case.
    /// </summary>
    /// <param name="value">The nullable value to convert.</param>
    /// <param name="defaultSecond">The second value to use if value is null.</param>
    /// <returns>An Either instance containing either the value or the default second value.</returns>
    /// <example>
    /// <code>
    /// string nullableValue = null;
    /// var either = Either&lt;string, string&gt;.FromNullable(
    ///     nullableValue,
    ///     "Value was null"
    /// );
    ///
    /// // With custom objects
    /// User user = await GetUserAsync(id);
    /// var result = Either&lt;User, string&gt;.FromNullable(
    ///     user,
    ///     $"User not found with id: {id}"
    /// );
    /// </code>
    /// </example>
    public static Either<T1, T2> FromNullable(T1 value, T2 defaultSecond)
    {
        return value != null ? new Either<T1, T2>(value) : new Either<T1, T2>(defaultSecond);
    }

    /// <summary>
    /// Attempts to execute a function that returns a value, wrapping the result or exception in an Either.
    /// </summary>
    /// <param name="func">The function to execute.</param>
    /// <returns>Either the successful result or the caught exception.</returns>
    /// <example>
    /// <code>
    /// // Parse number safely
    /// var result = Either&lt;int, Exception&gt;.Try(() => int.Parse("42"));
    ///
    /// // Handle database operations
    /// var dbResult = Either&lt;User, Exception&gt;.Try(() =>
    ///     dbContext.Users.First(u => u.Id == userId)
    /// );
    ///
    /// // Process result
    /// dbResult.Switch(
    ///     user => Console.WriteLine($"Found user: {user.Name}"),
    ///     ex => Console.WriteLine($"Error: {ex.Message}")
    /// );
    /// </code>
    /// </example>
    public static Either<T1, Exception> Try(Func<T1> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        try
        {
            return new Either<T1, Exception>(func());
        }
        catch (Exception ex)
        {
            return new Either<T1, Exception>(ex);
        }
    }

    /// <summary>
    /// Attempts to execute an async function, wrapping the result or exception in an Either.
    /// </summary>
    /// <param name="func">The async function to execute.</param>
    /// <returns>A task containing either the successful result or the caught exception.</returns>
    /// <example>
    /// <code>
    /// // Async API call with error handling
    /// var result = await Either&lt;ApiResponse, Exception&gt;.TryAsync(async () =>
    /// {
    ///     using var client = new HttpClient();
    ///     var response = await client.GetAsync("https://api.example.com/data");
    ///     response.EnsureSuccessStatusCode();
    ///     return await response.Content.ReadFromJsonAsync&lt;ApiResponse&gt;();
    /// });
    ///
    /// // Process result
    /// await result.SwitchAsync(
    ///     async response => await ProcessResponseAsync(response),
    ///     async ex => await LogErrorAsync(ex)
    /// );
    /// </code>
    /// </example>
    public static async Task<Either<T1, Exception>> TryAsync(Func<Task<T1>> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        try
        {
            return new Either<T1, Exception>(await func());
        }
        catch (Exception ex)
        {
            return new Either<T1, Exception>(ex);
        }
    }

    /// <summary>
    /// Filters the first value based on a predicate, converting to the second type if the predicate fails.
    /// </summary>
    /// <param name="predicate">The condition to test.</param>
    /// <param name="defaultSecond">The second value to use if the predicate fails.</param>
    /// <returns>A new Either instance.</returns>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Filter positive numbers
    /// var filtered = either.Filter(
    ///     n => n > 0,
    ///     "Number must be positive"
    /// );
    ///
    /// // Validate user input
    /// var userInput = new Either&lt;UserDto, string&gt;(dto);
    /// var validated = userInput.Filter(
    ///     dto => dto.Age >= 18,
    ///     "User must be 18 or older"
    /// );
    /// </code>
    /// </example>
    public Either<T1, T2> Filter(Func<T1, bool> predicate, T2 defaultSecond)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return this.Match(
            firstMatch: value => predicate(value) ? this : new Either<T1, T2>(defaultSecond),
            secondMatch: _ => this
        );
    }

    /// <summary>
    /// Converts an Either instance to a Result type, mapping both success and failure cases.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="firstMatch">Function to convert the first value type to the result value.</param>
    /// <param name="secondMatch">Function to convert the second value type to the result value.</param>
    /// <param name="errorFactory"></param>
    /// <returns>A Result instance containing the mapped value with appropriate success/failure state.</returns>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Convert to Result with value mapping
    /// var result = either.ToResult(
    ///     firstMatch: num => num.ToString(),  // Success case
    ///     secondMatch: error => "0"           // Error case
    /// );
    ///
    /// // Convert with error handling
    /// var result = either.ToResult(
    ///     firstMatch: num => new UserDto { Id = num },
    ///     secondMatch: error => default
    /// );
    ///
    /// // With domain validation
    /// var result = either.ToResult(
    ///     firstMatch: user => user,
    ///     secondMatch: error => default,
    ///     error => new ValidationError(error)
    /// );
    /// </code>
    /// </example>
    public Result<TResult> ToResult<TResult>(
        Func<T1, TResult> firstMatch,
        Func<T2, TResult> secondMatch,
        Func<T2, IResultError> errorFactory = null)
    {
        ArgumentNullException.ThrowIfNull(firstMatch);
        ArgumentNullException.ThrowIfNull(secondMatch);

        return this.Match(
            value => Result<TResult>.Success(firstMatch(value)),
            error =>
            {
                var result = Result<TResult>.Failure(secondMatch(error));
                if (errorFactory != null)
                {
                    result = result.WithError(errorFactory(error));
                }

                return result;
            });
    }

    /// <summary>
    /// Converts an Either instance to a Result type with error handling.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="firstMatch">Function to convert the first value type to the result value.</param>
    /// <param name="errorMessage">The error message to use when the Either contains the second type.</param>
    /// <returns>A Result instance containing the mapped value with appropriate success/failure state.</returns>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // Basic conversion with error message
    /// var result = either.ToResult(
    ///     firstMatch: num => num.ToString(),
    ///     errorMessage: "Failed to process value"
    /// );
    ///
    /// // With domain object
    /// var result = either.ToResult(
    ///     firstMatch: id => new User { Id = id },
    ///     errorMessage: "Invalid user ID"
    /// );
    /// </code>
    /// </example>
    public Result<TResult> ToResult<TResult>(
        Func<T1, TResult> firstMatch,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(firstMatch);

        return this.Match(
            value => Result<TResult>.Success(firstMatch(value)),
            _ => Result<TResult>.Failure(errorMessage));
    }

    /// <summary>
    /// Converts an Either instance to a Result type with custom error handling.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the error to create.</typeparam>
    /// <param name="firstMatch">Function to convert the first value type to the result value.</param>
    /// <returns>A Result instance containing the mapped value with appropriate success/failure state.</returns>
    /// <example>
    /// <code>
    /// var either = new Either&lt;int, string&gt;(42);
    ///
    /// // With custom error type
    /// var result = either.ToResult&lt;UserDto, ValidationError&gt;(
    ///     firstMatch: id => new UserDto { Id = id }
    /// );
    ///
    /// // With domain validation
    /// var result = either.ToResult&lt;User, DomainError&gt;(
    ///     firstMatch: id => new User { Id = id }
    /// );
    /// </code>
    /// </example>
    public Result<TResult> ToResult<TResult, TError>(Func<T1, TResult> firstMatch)
        where TError : IResultError, new()
    {
        ArgumentNullException.ThrowIfNull(firstMatch);

        return this.Match(
            value => Result<TResult>.Success(firstMatch(value)),
            _ => Result<TResult>.Failure<TError>());
    }
}

// a few usage examples
public class StringOrNumber : Either<string, int>
{
    public StringOrNumber(string value)
        : base(value) { }

    public StringOrNumber(int value)
        : base(value) { }
}

public struct None
{ }

public class InstanceOrNone<T> : Either<T, None>
{
    public InstanceOrNone(T value)
    {
        if (value != null)
        {
            this.Index = 1;
            this.Value1 = value;
        }
        else
        {
            this.Index = 2;
            this.Value2 = new None();
        }
    }
}

public class Exists : Either<Exists.Yes, Exists.No>
{
    public class Yes { }

    public class No { }

    public Exists() : base()
    {
        this.Index = 1;
        this.Value1 = new Yes();
    }

    public Exists(bool exists)
    {
        if (exists)
        {
            this.Index = 1;
            this.Value1 = new Yes();
        }
        else
        {
            this.Index = 2;
            this.Value2 = new No();
        }
    }
}

// Exists value = ...;
// value.Switch(
//     yes => do something,
//     no =>  do something
// );