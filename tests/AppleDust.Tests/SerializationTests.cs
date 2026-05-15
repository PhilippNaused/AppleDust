using AppleDust.Shared;

namespace AppleDust.Tests;

internal class SerializationTests
{
    public static (string, object)[] GetTestValues() =>
        [
            ("Hello%2C%0AWorld", "Hello,\nWorld"),
            ("", ""),
            ("1", 1),
            ("2", 2L),
            ("True", true),
            ("True", true),
            ("3.141", 3.141),
            ("1%2C2%2C3", new[] {1,2,3}),
            ("", Array.Empty<int>()),
            ("", Array.Empty<string>()),
            ("1%2C2%252C3%2C", new[] {[1], [2,3], Array.Empty<int>()}),
            ("H%2Ce%2Cy", new[] {"H","e","y"}),
            ("%2560%2C%257C%2C%252C%2C%250A", new[] {"`","|",",", "\n"}),
            ("7%2C8", (7L, 8L)),
            ("Hello%2CTrue%2C9", ("Hello", true, 9LU)),
            ("Hello%252C1%2CWorld%252C2", new []{("Hello", 1), ("World", 2)})
        ];

    [Test]
    [MethodDataSource(nameof(GetTestValues))]
    public async Task Serialize(string text, object value)
    {
        var actual = Utils.Serialize(value);
        await Assert.That(actual).IsEqualTo(text);
    }

    [Test]
    [MethodDataSource(nameof(GetTestValues))]
    public async Task Deserialize(string text, object value)
    {
        var type = value.GetType();
        var actual = Utils.Deserialize(text, type);
        await Assert.That(actual.GetType()).IsEqualTo(type);
        await Assert.That(actual).IsEquivalentTo(value);
    }

    [Test]
    [MethodDataSource(nameof(GetTestValues))]
    public async Task RoundTrip(string _, object value)
    {
        var text = Utils.Serialize(value);
        var type = value.GetType();
        var actual = Utils.Deserialize(text, type);
        await Assert.That(actual.GetType()).IsEqualTo(type);
        await Assert.That(actual).IsEquivalentTo(value);
    }
}
