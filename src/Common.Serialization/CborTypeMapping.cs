// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using System.Formats.Cbor;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;

/// <summary>Cached CBOR codecs and compiled object accessors. Reflection runs only when a type is first encountered.</summary>
internal sealed class CborTypeMapping(
    Action<CborSerializer, CborWriter, object, int> write,
    Func<CborSerializer, CborReader, int, object> read)
{
    private static readonly ConcurrentDictionary<Type, CborTypeMapping> Cache = [];

    internal Action<CborSerializer, CborWriter, object, int> Write { get; } = write;

    internal Func<CborSerializer, CborReader, int, object> Read { get; } = read;

    internal static CborTypeMapping Get(Type type)
    {
        return Cache.GetOrAdd(type, Create);
    }

    private static CborTypeMapping Scalar<T>(Action<CborWriter, T> writeValue, Func<CborReader, T> readValue)
    {
        return new((_, writer, value, _) => writeValue(writer, (T)value), (_, reader, _) => readValue(reader));
    }

    private static CborTypeMapping Create(Type type)
    {
        if (type == typeof(string)) return Scalar<string>((w, v) => w.WriteTextString(v), r => r.ReadTextString());
        if (type == typeof(byte[])) return Scalar<byte[]>((w, v) => w.WriteByteString(v), r => r.ReadByteString());
        if (type == typeof(ReadOnlyMemory<byte>)) return Scalar<ReadOnlyMemory<byte>>((w, v) => w.WriteByteString(v.Span), r => r.ReadByteString());
        if (type == typeof(Memory<byte>)) return Scalar<Memory<byte>>((w, v) => w.WriteByteString(v.Span), r => r.ReadByteString());
        if (type == typeof(nint)) return Scalar<nint>((w, v) => w.WriteInt64((long)v), r => checked((nint)r.ReadInt64()));
        if (type == typeof(nuint)) return Scalar<nuint>((w, v) => w.WriteUInt64((ulong)v), r => checked((nuint)r.ReadUInt64()));
        if (type == typeof(Uri)) return Scalar<Uri>((w, v) => w.WriteTextString(v.OriginalString), r => new Uri(r.ReadTextString(), UriKind.RelativeOrAbsolute));
        if (type == typeof(System.Version)) return Scalar<System.Version>((w, v) => w.WriteTextString(v.ToString()), r => new System.Version(r.ReadTextString()));
        if (type == typeof(ArraySegment<byte>)) return Scalar<ArraySegment<byte>>((w, v) => w.WriteByteString(v.AsSpan()), r => new ArraySegment<byte>(r.ReadByteString()));
        if (type == typeof(bool)) return Scalar<bool>((w, v) => w.WriteBoolean(v), r => r.ReadBoolean());
        if (type == typeof(byte)) return Scalar<byte>((w, v) => w.WriteUInt32(v), r => checked((byte)r.ReadUInt32()));
        if (type == typeof(sbyte)) return Scalar<sbyte>((w, v) => w.WriteInt32(v), r => checked((sbyte)r.ReadInt32()));
        if (type == typeof(short)) return Scalar<short>((w, v) => w.WriteInt32(v), r => checked((short)r.ReadInt32()));
        if (type == typeof(ushort)) return Scalar<ushort>((w, v) => w.WriteUInt32(v), r => checked((ushort)r.ReadUInt32()));
        if (type == typeof(int)) return Scalar<int>((w, v) => w.WriteInt32(v), r => r.ReadInt32());
        if (type == typeof(uint)) return Scalar<uint>((w, v) => w.WriteUInt32(v), r => r.ReadUInt32());
        if (type == typeof(long)) return Scalar<long>((w, v) => w.WriteInt64(v), r => r.ReadInt64());
        if (type == typeof(ulong)) return Scalar<ulong>((w, v) => w.WriteUInt64(v), r => r.ReadUInt64());
        if (type == typeof(Int128)) return Scalar<Int128>((w, v) => CborSerializer.WriteInteger(w, (BigInteger)v), r => checked((Int128)CborSerializer.ReadInteger(r)));
        if (type == typeof(UInt128)) return Scalar<UInt128>((w, v) => CborSerializer.WriteInteger(w, (BigInteger)v), r => checked((UInt128)CborSerializer.ReadInteger(r)));
        if (type == typeof(BigInteger)) return Scalar<BigInteger>(CborSerializer.WriteInteger, CborSerializer.ReadInteger);
        if (type == typeof(Half)) return Scalar<Half>((w, v) => w.WriteHalf(v), r => (Half)ReadFloatingPoint(r));
        if (type == typeof(float)) return Scalar<float>((w, v) => w.WriteSingle(v), r => (float)ReadFloatingPoint(r));
        if (type == typeof(double)) return Scalar<double>((w, v) => w.WriteDouble(v), ReadFloatingPoint);
        if (type == typeof(decimal)) return Scalar<decimal>((w, v) => w.WriteDecimal(v), r => r.PeekState() == CborReaderState.Tag ? r.ReadDecimal() : (decimal)CborSerializer.ReadInteger(r));
        if (type == typeof(char)) return Scalar<char>((w, v) => w.WriteTextString(v.ToString()), r => ReadChar(r));
        if (type == typeof(Guid)) return Scalar<Guid>(WriteGuid, ReadGuid);
        if (type == typeof(DateTimeOffset)) return Scalar<DateTimeOffset>((w, v) => w.WriteDateTimeOffset(v), ReadDateTimeOffset);
        if (type == typeof(DateTime)) return Scalar<DateTime>(WriteDateTime, ReadDateTime);
        if (type == typeof(TimeSpan)) return Scalar<TimeSpan>((w, v) => w.WriteTextString(v.ToString("c", CultureInfo.InvariantCulture)), r => TimeSpan.ParseExact(r.ReadTextString(), "c", CultureInfo.InvariantCulture));
        if (type == typeof(DateOnly)) return Scalar<DateOnly>((w, v) => w.WriteTextString(v.ToString("O", CultureInfo.InvariantCulture)), r => DateOnly.ParseExact(r.ReadTextString(), "O", CultureInfo.InvariantCulture));
        if (type == typeof(TimeOnly)) return Scalar<TimeOnly>((w, v) => w.WriteTextString(v.ToString("O", CultureInfo.InvariantCulture)), r => TimeOnly.ParseExact(r.ReadTextString(), "O", CultureInfo.InvariantCulture));

        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
        {
            return new((s, w, v, d) => s.WriteValue(w, v, d), (s, r, d) => s.ReadValue(r, nullable, d));
        }

        if (type.IsEnum)
        {
            var underlying = Enum.GetUnderlyingType(type);
            var value = Expression.Parameter(typeof(object));
            var convert = Expression.Lambda<Func<object, object>>(
                Expression.Convert(Expression.Convert(Expression.Convert(value, type), underlying), typeof(object)), value).Compile();
            var restore = Expression.Lambda<Func<object, object>>(
                Expression.Convert(Expression.Convert(Expression.Convert(value, underlying), type), typeof(object)), value).Compile();
            return new((s, w, v, d) => s.WriteValue(w, convert(v), d), (s, r, d) => restore(s.ReadValue(r, underlying, d)));
        }

        if (type.IsArray && type.GetArrayRank() != 1)
        {
            throw new NotSupportedException("Only one-dimensional CBOR arrays are supported.");
        }

        var dictionary = FindGeneric(type, typeof(IDictionary<,>)) ?? FindGeneric(type, typeof(IReadOnlyDictionary<,>));
        if (dictionary is not null)
        {
            return CreateGeneric(nameof(CreateDictionary), dictionary.GetGenericArguments(), type);
        }

        var enumerable = FindGeneric(type, typeof(IEnumerable<>));
        if (enumerable is not null)
        {
            return CreateGeneric(nameof(CreateCollection), enumerable.GetGenericArguments(), type);
        }

        if (typeof(System.Collections.IDictionary).IsAssignableFrom(type))
        {
            return CreateDictionary<object, object>(type);
        }

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            return CreateCollection<object>(type);
        }

        if (type.IsInterface || type.IsAbstract || type.IsPointer || type.IsByRefLike || typeof(Delegate).IsAssignableFrom(type))
        {
            throw new NotSupportedException($"CBOR object mapping requires a concrete supported type: {type}.");
        }

        return CreateObject(type);
    }

    private static Type FindGeneric(Type type, Type definition)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == definition
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition);
    }

    private static CborTypeMapping CreateGeneric(string method, Type[] arguments, Type type)
    {
        return (CborTypeMapping)typeof(CborTypeMapping).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(arguments).Invoke(null, [type]);
    }

    private static CborTypeMapping CreateCollection<T>(Type type)
    {
        Func<List<T>, object> finish;
        if (type.IsArray)
        {
            finish = values => values.ToArray();
        }
        else if (type.IsAssignableFrom(typeof(List<T>)))
        {
            finish = values => values;
        }
        else if (type.IsAssignableFrom(typeof(HashSet<T>)))
        {
            finish = values => new HashSet<T>(values);
        }
        else if (type == typeof(Stack<T>))
        {
            finish = values => new Stack<T>(values.AsEnumerable().Reverse());
        }
        else if (type == typeof(ConcurrentStack<T>))
        {
            finish = values => new ConcurrentStack<T>(values.AsEnumerable().Reverse());
        }
        else if (type == typeof(System.Collections.Stack))
        {
            finish = values => new System.Collections.Stack(values.AsEnumerable().Reverse().ToArray());
        }
        else
        {
            finish = CompileCollectionFactory<List<T>>(type);
        }

        return new(
            (serializer, writer, value, depth) =>
            {
                var values = value is IEnumerable<T> typed ? typed : ((System.Collections.IEnumerable)value).Cast<T>();
                var count = value is ICollection<T> collection ? collection.Count : value is IReadOnlyCollection<T> readOnly ? readOnly.Count : value is System.Collections.ICollection legacy ? legacy.Count : (int?)null;
                writer.WriteStartArray(count);
                foreach (var item in values)
                {
                    serializer.WriteValue(writer, item, depth + 1);
                }

                writer.WriteEndArray();
            },
            (serializer, reader, depth) =>
            {
                var length = reader.ReadStartArray();
                if (length > reader.BytesRemaining)
                {
                    throw new CborContentException("CBOR array length exceeds the remaining data.");
                }

                var values = new List<T>(length ?? 0);
                while (reader.PeekState() != CborReaderState.EndArray)
                {
                    values.Add((T)serializer.ReadValue(reader, typeof(T), depth + 1));
                }

                reader.ReadEndArray();
                return finish(values);
            });
    }

    private static CborTypeMapping CreateDictionary<TKey, TValue>(Type type)
    {
        var finish = type.IsAssignableFrom(typeof(Dictionary<TKey, TValue>))
            ? values => (object)values
            : CompileCollectionFactory<Dictionary<TKey, TValue>>(type, CreateMutableDictionaryFactory<TKey, TValue>(type));

        return new(
            (serializer, writer, value, depth) =>
            {
                var count = value is ICollection<KeyValuePair<TKey, TValue>> collection ? collection.Count
                    : value is IReadOnlyCollection<KeyValuePair<TKey, TValue>> readOnly ? readOnly.Count : value is System.Collections.ICollection legacy ? legacy.Count : (int?)null;
                writer.WriteStartMap(count);
                if (value is IEnumerable<KeyValuePair<TKey, TValue>> typed)
                {
                    foreach (var item in typed)
                    {
                        serializer.WriteValue(writer, item.Key, depth + 1);
                        serializer.WriteValue(writer, item.Value, depth + 1);
                    }
                }
                else
                {
                    foreach (System.Collections.DictionaryEntry item in (System.Collections.IDictionary)value)
                    {
                        serializer.WriteValue(writer, item.Key, depth + 1);
                        serializer.WriteValue(writer, item.Value, depth + 1);
                    }
                }

                writer.WriteEndMap();
            },
            (serializer, reader, depth) =>
            {
                var length = reader.ReadStartMap();
                if (length > reader.BytesRemaining / 2)
                {
                    throw new CborContentException("CBOR map length exceeds the remaining data.");
                }

                var values = new Dictionary<TKey, TValue>(length ?? 0);
                while (reader.PeekState() != CborReaderState.EndMap)
                {
                    var key = (TKey)serializer.ReadValue(reader, typeof(TKey), depth + 1);
                    var value = (TValue)serializer.ReadValue(reader, typeof(TValue), depth + 1);
                    if (key is null || !values.TryAdd(key, value))
                    {
                        throw new CborContentException("CBOR dictionary keys must be non-null and unique.");
                    }
                }

                reader.ReadEndMap();
                return finish(values);
            });
    }

    private static Func<TCollection, object> CompileCollectionFactory<TCollection>(Type type, Func<TCollection, object> populate = null)
    {
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(c => c.GetParameters() is [var p] && p.ParameterType.IsAssignableFrom(typeof(TCollection)));
        if (constructor is null)
        {
            if (populate is not null)
            {
                return populate;
            }

            // Some mutable collections expose only a parameterless constructor and Add.
            var create = CompileDefaultConstructor(type);
            if (create is not null && typeof(TCollection).IsGenericType && typeof(TCollection).GetGenericTypeDefinition() == typeof(List<>))
            {
                return CompileMutableCollectionFactory<TCollection>(type, create);
            }

            return _ => throw new NotSupportedException($"CBOR cannot construct collection {type} from its values.");
        }

        var values = Expression.Parameter(typeof(TCollection));
        return Expression.Lambda<Func<TCollection, object>>(
            Expression.Convert(Expression.New(constructor, Expression.Convert(values, constructor.GetParameters()[0].ParameterType)), typeof(object)), values).Compile();
    }

    private static Func<Dictionary<TKey, TValue>, object> CreateMutableDictionaryFactory<TKey, TValue>(Type type)
    {
        var generic = typeof(IDictionary<TKey, TValue>).IsAssignableFrom(type);
        var legacy = typeof(System.Collections.IDictionary).IsAssignableFrom(type);
        if (!generic && !legacy)
        {
            return null;
        }

        var create = CompileDefaultConstructor(type);
        if (create is null)
        {
            return null;
        }

        if (generic)
        {
            return values =>
            {
                var result = (IDictionary<TKey, TValue>)create();
                foreach (var entry in values)
                {
                    result.Add(entry.Key, entry.Value);
                }

                return result;
            };
        }

        return values =>
        {
            var result = (System.Collections.IDictionary)create();
            foreach (var entry in values)
            {
                result.Add(entry.Key, entry.Value);
            }

            return result;
        };
    }

    private static Func<TCollection, object> CompileMutableCollectionFactory<TCollection>(Type type, Func<object> create)
    {
        var element = typeof(TCollection).GetGenericArguments()[0];
        var collection = typeof(ICollection<>).MakeGenericType(element);
        if (!collection.IsAssignableFrom(type))
        {
            return _ => throw new NotSupportedException($"CBOR cannot populate collection {type}.");
        }

        var target = Expression.Parameter(typeof(object));
        var item = Expression.Parameter(typeof(object));
        var add = Expression.Lambda<Action<object, object>>(
            Expression.Call(Expression.Convert(target, collection), collection.GetMethod("Add"), Expression.Convert(item, element)), target, item).Compile();
        return values =>
        {
            var result = create();
            foreach (var value in (System.Collections.IEnumerable)values)
            {
                add(result, value);
            }

            return result;
        };
    }

    private static CborTypeMapping CreateObject(Type type)
    {
        var members = new List<Member>();
        var candidates = new List<MemberInfo>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            candidates.AddRange(current.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m switch
                {
                    PropertyInfo p => p.GetMethod is not null && p.GetIndexParameters().Length == 0
                        && (p.GetMethod.IsPublic || p.IsDefined(typeof(DataMemberAttribute))),
                    FieldInfo f => f.IsPublic || f.IsDefined(typeof(DataMemberAttribute)),
                    _ => false
                }));
        }

        var optIn = type.IsDefined(typeof(DataContractAttribute), false);
        foreach (var candidate in candidates.DistinctBy(m => m.Name, StringComparer.Ordinal))
        {
            var contract = candidate.GetCustomAttribute<DataMemberAttribute>();
            if (candidate.IsDefined(typeof(IgnoreDataMemberAttribute)) || optIn && contract is null)
            {
                continue;
            }

            var name = contract?.Name ?? candidate.Name;
            var target = Expression.Parameter(typeof(object));
            var instance = type.IsValueType ? Expression.Unbox(target, type) : Expression.Convert(target, candidate.DeclaringType);
            Action<object, object> set = null;
            if (candidate is PropertyInfo property)
            {
                var get = Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Property(instance, property), typeof(object)), target).Compile();
                if (property.SetMethod is not null)
                {
                    var value = Expression.Parameter(typeof(object));
                    set = Expression.Lambda<Action<object, object>>(
                        Expression.Call(instance, property.SetMethod, Expression.Convert(value, property.PropertyType)), target, value).Compile();
                }

                members.Add(new(name, property.Name, property.PropertyType, get, set));
            }
            else if (candidate is FieldInfo field)
            {
                var access = Expression.Field(instance, field);
                var get = Expression.Lambda<Func<object, object>>(Expression.Convert(access, typeof(object)), target).Compile();
                if (!field.IsInitOnly)
                {
                    var value = Expression.Parameter(typeof(object));
                    set = Expression.Lambda<Action<object, object>>(
                        Expression.Assign(access, Expression.Convert(value, field.FieldType)), target, value).Compile();
                }

                members.Add(new(name, field.Name, field.FieldType, get, set));
            }
        }

        var ordered = members.OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
        if (ordered.Select(m => m.Name).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new NotSupportedException($"CBOR member names must be unique for {type}.");
        }

        var lookup = ordered.Select((m, i) => (m.Name, Index: i)).ToDictionary(x => x.Name, x => x.Index, StringComparer.Ordinal);
        var create = CompileDefaultConstructor(type);
        var construct = CompileParameterizedConstructor(type, ordered, out var constructorMembers, out var explicitConstructor);
        if (explicitConstructor || type.IsValueType && ordered.Any(m => m.Set is null) && constructorMembers.Count > 0)
        {
            create = null;
        }

        return new(
            (serializer, writer, value, depth) =>
            {
                writer.WriteStartMap(ordered.Length);
                foreach (var member in ordered)
                {
                    writer.WriteTextString(member.Name);
                    serializer.WriteValue(writer, member.Get(value), depth + 1);
                }

                writer.WriteEndMap();
            },
            (serializer, reader, depth) =>
            {
                reader.ReadStartMap();
                var instance = create?.Invoke();
                var values = create is null ? new object[ordered.Length] : null;
                var present = create is null ? new bool[ordered.Length] : null;
                var seen = ordered.Length <= 128 ? stackalloc byte[ordered.Length] : new byte[ordered.Length];
                seen.Clear();
                HashSet<string> unknownNames = null;
                Span<char> nameBuffer = stackalloc char[128];
                var names = lookup.GetAlternateLookup<ReadOnlySpan<char>>();
                while (reader.PeekState() != CborReaderState.EndMap)
                {
                    int index;
                    string name = null;
                    var found = reader.TryReadTextString(nameBuffer, out var length)
                        ? names.TryGetValue(nameBuffer[..length], out index)
                        : lookup.TryGetValue(name = reader.ReadTextString(), out index);
                    if (found)
                    {
                        if (seen[index] != 0)
                        {
                            throw new CborContentException("CBOR object member names must be unique.");
                        }

                        seen[index] = 1;
                    }
                    else if (!(unknownNames ??= new(StringComparer.Ordinal)).Add(name ?? new string(nameBuffer[..length])))
                    {
                        throw new CborContentException("CBOR object member names must be unique.");
                    }

                    if (found && (values is not null || ordered[index].Set is not null))
                    {
                        var value = serializer.ReadValue(reader, ordered[index].Type, depth + 1);
                        if (values is not null)
                        {
                            values[index] = value;
                            present[index] = true;
                        }
                        else
                        {
                            ordered[index].Set(instance, value);
                        }
                    }
                    else
                    {
                        serializer.SkipValue(reader, depth + 1);
                    }
                }

                reader.ReadEndMap();
                if (values is not null)
                {
                    instance = construct(values, present);
                    for (var i = 0; i < ordered.Length; i++)
                    {
                        if (present[i] && !constructorMembers.Contains(i))
                        {
                            ordered[i].Set?.Invoke(instance, values[i]);
                        }
                    }
                }

                return instance;
            });
    }

    private static Func<object> CompileDefaultConstructor(Type type)
    {
        var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (constructor is null && type.IsValueType)
        {
            return Expression.Lambda<Func<object>>(Expression.Convert(Expression.Default(type), typeof(object))).Compile();
        }

        return constructor is null ? null : Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructor), typeof(object))).Compile();
    }

    private static Func<object[], bool[], object> CompileParameterizedConstructor(Type type, Member[] members, out HashSet<int> constructorMembers, out bool explicitConstructor)
    {
        var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var marked = constructors.Where(c => c.IsDefined(typeof(CborConstructorAttribute))).ToArray();
        if (marked.Length > 1)
        {
            throw new NotSupportedException($"Only one CBOR constructor may be selected for {type}.");
        }

        explicitConstructor = marked.Length == 1;
        var constructor = explicitConstructor ? marked[0] : constructors.OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault(c => CanBindConstructor(c, members));
        if (explicitConstructor && !CanBindConstructor(constructor, members))
        {
            throw new NotSupportedException($"The selected CBOR constructor for {type} must match CLR member names and types.");
        }

        constructorMembers = [];
        if (constructor is null)
        {
            return (_, _) => throw new NotSupportedException($"CBOR cannot bind a constructor for {type}.");
        }

        foreach (var parameter in constructor.GetParameters())
        {
            var index = Array.FindIndex(members, m => string.Equals(m.ClrName, parameter.Name, StringComparison.OrdinalIgnoreCase) && m.Type == parameter.ParameterType);
            if (index >= 0)
            {
                constructorMembers.Add(index);
            }
        }

        var values = Expression.Parameter(typeof(object[]));
        var present = Expression.Parameter(typeof(bool[]));
        var arguments = constructor.GetParameters().Select(p =>
        {
            var index = Array.FindIndex(members, m => string.Equals(m.ClrName, p.Name, StringComparison.OrdinalIgnoreCase) && m.Type == p.ParameterType);
            Expression fallback = p.HasDefaultValue && p.DefaultValue is not (null or DBNull or Missing)
                ? Expression.Convert(Expression.Constant(p.DefaultValue), p.ParameterType)
                : Expression.Default(p.ParameterType);
            if (index < 0)
            {
                return fallback;
            }

            var value = Expression.ArrayIndex(values, Expression.Constant(index));
            return Expression.Condition(Expression.ArrayIndex(present, Expression.Constant(index)), Expression.Convert(value, p.ParameterType), fallback);
        });
        return Expression.Lambda<Func<object[], bool[], object>>(Expression.Convert(Expression.New(constructor, arguments), typeof(object)), values, present).Compile();
    }

    private static bool CanBindConstructor(ConstructorInfo constructor, Member[] members)
    {
        return constructor.GetParameters().All(p => p.HasDefaultValue || members.Any(m =>
            string.Equals(m.ClrName, p.Name, StringComparison.OrdinalIgnoreCase) && m.Type == p.ParameterType));
    }

    private static double ReadFloatingPoint(CborReader reader)
    {
        return reader.PeekState() switch
        {
            CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger => (double)CborSerializer.ReadInteger(reader),
            CborReaderState.Tag => (double)reader.ReadDecimal(),
            _ => reader.ReadDouble()
        };
    }

    private static char ReadChar(CborReader reader)
    {
        var value = reader.ReadTextString();
        return value.Length == 1 ? value[0] : throw new CborContentException("Expected one UTF-16 character.");
    }

    private static void WriteGuid(CborWriter writer, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        writer.WriteTag((CborTag)37);
        writer.WriteByteString(bytes);
    }

    private static Guid ReadGuid(CborReader reader)
    {
        RequireTag(reader, (CborTag)37);
        var bytes = reader.ReadByteString();
        return bytes.Length == 16 ? new Guid(bytes, bigEndian: true) : throw new CborContentException("A UUID must contain 16 bytes.");
    }

    private static void WriteDateTime(CborWriter writer, DateTime value)
    {
        // Unspecified dates have no UTC offset and therefore are not RFC 3339 tagged dates.
        if (value.Kind != DateTimeKind.Unspecified)
        {
            writer.WriteTag(CborTag.DateTimeString);
        }

        writer.WriteTextString(value.ToString("O", CultureInfo.InvariantCulture));
    }

    private static DateTime ReadDateTime(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Tag)
        {
            if (reader.PeekTag() == CborTag.UnixTimeSeconds)
            {
                return reader.ReadUnixTimeSeconds().UtcDateTime;
            }

            RequireTag(reader, CborTag.DateTimeString);
        }

        return DateTime.Parse(reader.ReadTextString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static DateTimeOffset ReadDateTimeOffset(CborReader reader)
    {
        return reader.PeekTag() == CborTag.UnixTimeSeconds ? reader.ReadUnixTimeSeconds() : reader.ReadDateTimeOffset();
    }

    private static void RequireTag(CborReader reader, CborTag tag)
    {
        if (reader.ReadTag() != tag)
        {
            throw new CborContentException($"Expected CBOR tag {tag}.");
        }
    }

    private sealed record Member(string Name, string ClrName, Type Type, Func<object, object> Get, Action<object, object> Set);
}