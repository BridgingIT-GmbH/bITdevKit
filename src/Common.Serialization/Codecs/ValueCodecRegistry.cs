// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Describes one trusted generic codec registration without loading types from persisted identifiers.</summary>
/// <example><code>var registration = new ValueCodecRegistration&lt;ProductCard&gt;(codec);</code></example>
public interface IValueCodecRegistration
{
    /// <summary>Gets the trusted CLR contract registered in code.</summary>
    Type ValueType { get; }

    /// <summary>Gets the representation read by the registered codec.</summary>
    ValueCodecFormat Format { get; }

    /// <summary>Gets the trusted codec instance.</summary>
    object Codec { get; }
}

/// <summary>Registers a typed codec using a statically selected value contract.</summary>
/// <typeparam name="T">The registered contract.</typeparam>
/// <param name="codec">The implementation for this exact format.</param>
/// <example><code>var registration = new ValueCodecRegistration&lt;ProductCard&gt;(codec);</code></example>
public sealed class ValueCodecRegistration<T>(IValueCodec<T> codec) : IValueCodecRegistration
{
    /// <inheritdoc />
    public Type ValueType => typeof(T);

    /// <inheritdoc />
    public ValueCodecFormat Format => new(codec.FormatId, codec.FormatVersion);

    /// <inheritdoc />
    public object Codec => codec;
}

/// <summary>Resolves an immutable set of explicitly registered format readers.</summary>
/// <param name="registrations">Trusted code registrations; duplicate contract/format pairs are rejected.</param>
/// <example><code>if (registry.TryGet&lt;ProductCard&gt;(new("json", 1), out var codec)) { /* decode */ }</code></example>
public sealed class ValueCodecRegistry(IEnumerable<IValueCodecRegistration> registrations)
{
    private readonly IReadOnlyDictionary<(Type Type, ValueCodecFormat Format), object> readers = Build(registrations);

    /// <summary>Finds only an explicitly registered reader for the requested type and byte format.</summary>
    /// <typeparam name="T">The trusted caller's contract.</typeparam>
    /// <param name="format">The persisted format identifier/version.</param>
    /// <param name="codec">The matching implementation, or null.</param>
    /// <returns>Whether a registered matching reader exists.</returns>
    public bool TryGet<T>(ValueCodecFormat format, out IValueCodec<T> codec)
    {
        codec = this.readers.TryGetValue((typeof(T), format), out var reader) ? (IValueCodec<T>)reader : null;
        return codec is not null;
    }

    private static IReadOnlyDictionary<(Type, ValueCodecFormat), object> Build(IEnumerable<IValueCodecRegistration> registrations)
    {
        var result = new Dictionary<(Type, ValueCodecFormat), object>();
        foreach (var registration in registrations ?? [])
        {
            if (registration.Codec is null || string.IsNullOrWhiteSpace(registration.Format.Id) || registration.Format.Version < 1)
            {
                throw new ArgumentException("A codec registration must declare a value contract and positive format version.", nameof(registrations));
            }

            if (!result.TryAdd((registration.ValueType, registration.Format), registration.Codec))
            {
                throw new ArgumentException("A codec reader for the same contract and format is already registered.", nameof(registrations));
            }
        }

        return result;
    }
}