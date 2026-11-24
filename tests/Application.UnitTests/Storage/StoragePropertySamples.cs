namespace BridgingIT.DevKit.Application.UnitTests.Storage;

public static class StoragePropertySamples
{
    public static PropertyBag Create() => new()
    {
        ["Null"] = null, ["String"] = "123", ["Unicode / \u540d"] = "h\u00e9llo", ["Char"] = 'x',
        ["Bool"] = true, ["Byte"] = (byte)1, ["SByte"] = (sbyte)-2,
        ["Short"] = (short)-3, ["UShort"] = (ushort)4, ["Int"] = -5, ["UInt"] = 6U,
        ["Long"] = -7L, ["ULong"] = 8UL, ["Float"] = 1.25f, ["Double"] = 2.5d,
        ["Decimal"] = 123456789.0123456789m, ["DecimalScale"] = 123.4500m, ["Guid"] = Guid.Parse("e22261d9-2719-4772-b6c4-d8ea1a941b5e"),
        ["DateTime"] = new DateTime(2026, 11, 16, 12, 34, 56, DateTimeKind.Unspecified),
        ["DateTimeOffset"] = new DateTimeOffset(2026, 11, 16, 12, 34, 56, TimeSpan.FromHours(2)),
        ["DateOnly"] = new DateOnly(2026, 11, 16), ["TimeOnly"] = new TimeOnly(12, 34, 56),
        ["TimeSpan"] = TimeSpan.FromTicks(12345), ["Bytes"] = new byte[] { 1, 2, 3 }
    };

    public static void AssertEqual(PropertyBag actual, PropertyBag expected)
    {
        actual.Count.ShouldBe(expected.Count);
        foreach (var item in expected)
        {
            actual.Contains(item.Key).ShouldBeTrue();
            var value = actual.Get(item.Key);
            value?.GetType().ShouldBe(item.Value?.GetType());
            if (item.Value is byte[] bytes) ((byte[])value).ShouldBe(bytes);
            else value.ShouldBe(item.Value);
            if (item.Value is DateTime date) ((DateTime)value).Kind.ShouldBe(date.Kind);
            if (item.Value is DateTimeOffset offset) ((DateTimeOffset)value).Offset.ShouldBe(offset.Offset);
            if (item.Value is decimal number) decimal.GetBits((decimal)value).ShouldBe(decimal.GetBits(number));
        }
    }
}
