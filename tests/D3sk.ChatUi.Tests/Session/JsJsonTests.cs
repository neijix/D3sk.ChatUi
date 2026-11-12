using System.Text.Json.Nodes;
using D3sk.ChatUi.Session;

namespace D3sk.ChatUi.Tests.Session;

/// <summary>Values checked against node: <c>(x).toFixed(1)</c> and <c>JSON.stringify(v, null, 2)</c>.</summary>
public sealed class JsJsonTests
{
    [Theory]
    [InlineData(1.25, "1.3")]
    [InlineData(1.15, "1.1")]
    [InlineData(1.05, "1.1")]
    [InlineData(0.05, "0.1")]
    [InlineData(2.45, "2.5")]
    [InlineData(10.2345, "10.2")]
    [InlineData(1, "1.0")]
    [InlineData(999.95, "1000.0")]
    public void To_fixed_rounds_like_javascript(double value, string expected) =>
        Assert.Equal(expected, JsJson.ToFixed(value, 1));

    [Fact]
    public void Stringify_writes_like_javascript()
    {
        var node = JsonNode.Parse("""{"q":"issue 7","n":3,"deep":{"a":[1,2.5,true,null],"é":"ü\n\"x\"\\"},"e":{},"l":[]}""");
        Assert.Equal(
            "{\n  \"q\": \"issue 7\",\n  \"n\": 3,\n  \"deep\": {\n    \"a\": [\n      1,\n      2.5,\n      true,\n      null\n    ],\n    \"é\": \"ü\\n\\\"x\\\"\\\\\"\n  },\n  \"e\": {},\n  \"l\": []\n}",
            JsJson.Stringify(node, 2));
    }
}
