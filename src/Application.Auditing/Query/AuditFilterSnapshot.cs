// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Copies filter input without retaining mutable caller objects or invoking object serialization.</summary>
/// <example><code>var snapshot = AuditFilterSnapshot.Copy(new FilterModel { Filters = [new("ModuleName", FilterOperator.Equal, "Sales")] });</code></example>
public static class AuditFilterSnapshot
{
    private const int MAX_TEXT_BYTES = 64 * 1024;
    private const int MAX_COLLECTION_ITEMS = 1024;
    private const int MAX_NODES = 4096;

    /// <summary>Creates an independent, bounded copy, preserving null input and null members.</summary>
    /// <param name="source">The model to copy, or null.</param>
    /// <param name="maxCriteria">The combined criterion and specification-node limit, between 1 and 1024.</param>
    /// <param name="maxDepth">The maximum nested criterion, specification, or value depth, between 1 and 64.</param>
    /// <returns>A deep copy, or null when the source is null.</returns>
    /// <exception cref="ArgumentException">The input has unsupported values, invalid structure, or exceeds a limit.</exception>
    /// <remarks>Values accept immutable audit scalars, JSON, one-dimensional scalar or object arrays, standard string-keyed dictionaries, and filter criteria. Dictionaries in values become ordinal string/object dictionaries. Total text and scalar value data is limited to 64 KiB, collections to 1024 items, and the copied graph to 4096 nodes.</remarks>
    /// <example><code>var snapshot = AuditFilterSnapshot.Copy(filter, maxCriteria: 32, maxDepth: 8);</code></example>
    public static FilterModel Copy(FilterModel source, int maxCriteria = 32, int maxDepth = 8)
    {
        if (maxCriteria is < 1 or > MAX_COLLECTION_ITEMS || maxDepth is < 1 or > 64)
        {
            throw new ArgumentException("Invalid filter snapshot limits.");
        }

        if (source is null)
        {
            return null;
        }

        try
        {
            return new CopyContext(maxCriteria, maxDepth).CopyModel(source);
        }
        catch (Exception exception) when (exception is EncoderFallbackException or JsonException or InvalidOperationException)
        {
            throw new ArgumentException("Invalid filter snapshot structure.");
        }
    }

    private sealed class CopyContext(int maxCriteria, int maxDepth)
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private readonly HashSet<object> active = new(ReferenceEqualityComparer.Instance);
        private int criteria;
        private int nodes;
        private int bytes;
        private JsonBuffer jsonBuffer;

        public FilterModel CopyModel(FilterModel source)
        {
            this.Enter(source, 0);
            this.RequireType(source, typeof(FilterModel));
            var result = new FilterModel
            {
                Page = source.Page,
                PageSize = source.PageSize,
                NoTracking = source.NoTracking,
                Hierarchy = this.CopyText(source.Hierarchy),
                HierarchyMaxDepth = source.HierarchyMaxDepth,
                Filters = this.CopyList(source.Filters, item => this.CopyCriteria(item, 1)),
                Orderings = this.CopyList(source.Orderings, this.CopyOrdering),
                Includes = this.CopyList(source.Includes, this.CopyText)
            };
            this.Exit(source);

            return result;
        }

        private FilterOrderCriteria CopyOrdering(FilterOrderCriteria source)
        {
            if (source is null)
            {
                this.Charge(1);
                return null;
            }

            this.Enter(source, 0);
            this.RequireType(source, typeof(FilterOrderCriteria));
            this.CheckEnum(source.Direction);
            var result = new FilterOrderCriteria { Field = this.CopyText(source.Field), Direction = source.Direction };
            this.Exit(source);

            return result;
        }

        private FilterCriteria CopyCriteria(FilterCriteria source, int depth)
        {
            if (source is null)
            {
                this.Charge(1);
                return null;
            }

            this.Enter(source, depth);
            this.CountCriterion();
            this.RequireType(source, typeof(FilterCriteria));
            this.CheckEnum(source.Operator);
            this.CheckEnum(source.Logic);
            this.CheckEnum(source.CustomType);
            var result = new FilterCriteria
            {
                Field = this.CopyText(source.Field),
                Operator = source.Operator,
                Logic = source.Logic,
                CustomType = source.CustomType,
                Value = this.CopyValue(source.Value, depth),
                Filters = this.CopyList(source.Filters, item => this.CopyCriteria(item, depth + 1)),
                CustomParameters = source.CustomParameters is null ? null : this.CopyDictionary(source.CustomParameters, depth),
                SpecificationName = this.CopyText(source.SpecificationName),
                SpecificationArguments = source.SpecificationArguments is null ? null : (object[])this.CopyArray(source.SpecificationArguments, depth),
                CompositeSpecification = this.CopyComposite(source.CompositeSpecification, depth)
            };
            this.Exit(source);

            return result;
        }

        private CompositeSpecification CopyComposite(CompositeSpecification source, int depth)
        {
            if (source is null)
            {
                return null;
            }

            this.Enter(source, depth);
            this.CountCriterion();
            this.RequireType(source, typeof(CompositeSpecification));
            var result = new CompositeSpecification { Nodes = this.CopyList(source.Nodes, node => this.CopySpecification(node, depth + 1)) };
            this.Exit(source);

            return result;
        }

        private SpecificationNode CopySpecification(SpecificationNode source, int depth)
        {
            if (source is null)
            {
                this.Charge(1);
                return null;
            }

            this.Enter(source, depth);
            this.CountCriterion();
            SpecificationNode result;
            if (source.GetType() == typeof(SpecificationLeaf))
            {
                var leaf = (SpecificationLeaf)source;
                result = new SpecificationLeaf
                {
                    Name = this.CopyText(leaf.Name),
                    Arguments = leaf.Arguments is null ? null : (object[])this.CopyArray(leaf.Arguments, depth)
                };
            }
            else if (source.GetType() == typeof(SpecificationGroup))
            {
                var group = (SpecificationGroup)source;
                this.CheckEnum(group.Logic);
                result = new SpecificationGroup
                {
                    Logic = group.Logic,
                    Nodes = this.CopyList(group.Nodes, node => this.CopySpecification(node, depth + 1))
                };
            }
            else
            {
                throw new ArgumentException("Unsupported filter snapshot type.");
            }

            this.Exit(source);

            return result;
        }

        private List<T> CopyList<T>(List<T> source, Func<T, T> copy)
        {
            if (source is null)
            {
                return null;
            }

            this.Enter(source, 0);
            this.RequireType(source, typeof(List<T>));
            this.CheckCount(source.Count);
            var result = new List<T>(source.Count);
            foreach (var item in source)
            {
                result.Add(copy(item));
            }

            this.Exit(source);

            return result;
        }

        private object CopyValue(object source, int depth)
        {
            this.CountNode();
            switch (source)
            {
                case null:
                    this.Charge(1);
                    return null;
                case string text:
                    return this.CopyText(text);
                case bool:
                    this.Charge(5);
                    return source;
                case byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                    this.Charge(((IFormattable)source).ToString(null, CultureInfo.InvariantCulture).Length);
                    return source;
                case float number when float.IsFinite(number):
                    this.Charge(number.ToString("R", CultureInfo.InvariantCulture).Length);
                    return number;
                case double number when double.IsFinite(number):
                    this.Charge(number.ToString("R", CultureInfo.InvariantCulture).Length);
                    return number;
                case Guid:
                    this.Charge(36);
                    return source;
                case DateTime or DateTimeOffset:
                    this.Charge(33);
                    return source;
                case JsonElement json:
                    return this.CopyJson(json, depth);
                case FilterCriteria criterion:
                    return this.CopyCriteria(criterion, depth + 1);
                case Array array:
                    return this.CopyArray(array, depth + 1);
                case IDictionary dictionary when IsStandardDictionary(source.GetType()):
                    return this.CopyDictionary(dictionary, depth + 1);
                default:
                    throw new ArgumentException("Unsupported filter snapshot value.");
            }
        }

        private Array CopyArray(Array source, int depth)
        {
            this.Enter(source, depth);
            if (!source.GetType().IsSZArray || !IsArrayElement(source.GetType().GetElementType()))
            {
                throw new ArgumentException("Unsupported filter snapshot array.");
            }

            this.CheckCount(source.Length);
            var result = (Array)source.Clone();
            for (var index = 0; index < source.Length; index++)
            {
                result.SetValue(this.CopyValue(source.GetValue(index), depth), index);
            }

            this.Exit(source);

            return result;
        }

        private Dictionary<string, object> CopyDictionary(IDictionary source, int depth)
        {
            this.Enter(source, depth);
            if (!IsStandardDictionary(source.GetType()))
            {
                throw new ArgumentException("Unsupported filter snapshot dictionary.");
            }

            this.CheckCount(source.Count);
            var result = new Dictionary<string, object>(source.Count, StringComparer.Ordinal);
            foreach (DictionaryEntry item in source)
            {
                var key = this.CopyText((string)item.Key);
                if (!result.TryAdd(key, this.CopyValue(item.Value, depth)))
                {
                    throw new ArgumentException("Invalid filter snapshot dictionary.");
                }
            }

            this.Exit(source);

            return result;
        }

        private JsonElement CopyJson(JsonElement source, int depth)
        {
            var buffer = this.jsonBuffer ??= new JsonBuffer();
            buffer.Reset();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                source.WriteTo(writer);
                writer.Flush();
            }

            this.Charge(buffer.WrittenCount);
            this.ValidateJson(source, depth);

            return source.Clone();
        }

        private void ValidateJson(JsonElement source, int depth)
        {
            this.CheckDepth(depth);
            this.CountNode();
            switch (source.ValueKind)
            {
                case JsonValueKind.Object:
                    var count = 0;
                    foreach (var property in source.EnumerateObject())
                    {
                        this.CheckCount(++count);
                        _ = StrictUtf8.GetByteCount(property.Name);
                        this.ValidateJson(property.Value, depth + 1);
                    }

                    break;
                case JsonValueKind.Array:
                    this.CheckCount(source.GetArrayLength());
                    foreach (var item in source.EnumerateArray())
                    {
                        this.ValidateJson(item, depth + 1);
                    }

                    break;
                case JsonValueKind.String:
                    _ = StrictUtf8.GetByteCount(source.GetString());
                    break;
                case JsonValueKind.Number:
                    if (!source.TryGetDouble(out var number) || !double.IsFinite(number))
                    {
                        throw new ArgumentException("Invalid filter snapshot number.");
                    }

                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    break;
                default:
                    throw new ArgumentException("Invalid filter snapshot JSON.");
            }
        }

        private string CopyText(string source)
        {
            if (source is null)
            {
                return null;
            }

            if (source.Length > MAX_TEXT_BYTES)
            {
                throw new ArgumentException("Filter snapshot value budget exceeded.");
            }

            this.Charge(StrictUtf8.GetByteCount(source));

            return source;
        }

        private void Enter(object source, int depth)
        {
            this.CheckDepth(depth);
            this.CountNode();
            if (!this.active.Add(source))
            {
                throw new ArgumentException("Cyclic filter snapshot structure.");
            }
        }

        private void Exit(object source) => this.active.Remove(source);

        private void CountCriterion()
        {
            if (++this.criteria > maxCriteria)
            {
                throw new ArgumentException("Filter snapshot criterion limit exceeded.");
            }
        }

        private void CountNode()
        {
            if (++this.nodes > MAX_NODES)
            {
                throw new ArgumentException("Filter snapshot node limit exceeded.");
            }
        }

        private void Charge(int count)
        {
            if (count > MAX_TEXT_BYTES - this.bytes)
            {
                throw new ArgumentException("Filter snapshot value budget exceeded.");
            }

            this.bytes += count;
        }

        private void CheckDepth(int depth)
        {
            if (depth > maxDepth)
            {
                throw new ArgumentException("Filter snapshot depth limit exceeded.");
            }
        }

        private void CheckCount(int count)
        {
            if (count > MAX_COLLECTION_ITEMS)
            {
                throw new ArgumentException("Filter snapshot collection limit exceeded.");
            }
        }

        private void RequireType(object source, Type expected)
        {
            if (source.GetType() != expected)
            {
                throw new ArgumentException("Unsupported filter snapshot type.");
            }
        }

        private void CheckEnum<T>(T value) where T : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentException("Invalid filter snapshot enumeration.");
            }
        }

        private static bool IsStandardDictionary(Type type) => type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GetGenericArguments()[0] == typeof(string);

        private static bool IsArrayElement(Type type) => type == typeof(object) || type == typeof(string) ||
            type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
            type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long) ||
            type == typeof(ulong) || type == typeof(decimal) || type == typeof(float) || type == typeof(double) ||
            type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(JsonElement) || type == typeof(FilterCriteria);
    }

    private sealed class JsonBuffer : IBufferWriter<byte>
    {
        private readonly byte[] buffer = new byte[MAX_TEXT_BYTES];

        public int WrittenCount { get; private set; }

        public void Reset() => this.WrittenCount = 0;

        public void Advance(int count)
        {
            if (count < 0 || count > this.buffer.Length - this.WrittenCount)
            {
                throw new ArgumentException("Filter snapshot value budget exceeded.");
            }

            this.WrittenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            this.CheckSize(sizeHint);
            return this.buffer.AsMemory(this.WrittenCount);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            this.CheckSize(sizeHint);
            return this.buffer.AsSpan(this.WrittenCount);
        }

        private void CheckSize(int sizeHint)
        {
            if (sizeHint < 0 || Math.Max(1, sizeHint) > this.buffer.Length - this.WrittenCount)
            {
                throw new ArgumentException("Filter snapshot value budget exceeded.");
            }
        }
    }
}