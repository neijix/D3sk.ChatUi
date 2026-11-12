using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace D3sk.ChatUi.Session;

/// <summary>
/// JSON written the way JavaScript's <c>JSON.stringify(value, null, indent)</c> writes it, so a tool's
/// IN row reads the same in the web chat and here (key order kept, only <c>"</c>, <c>\</c> and control
/// characters escaped, numbers in their shortest form).
/// </summary>
public static class JsJson
{
    /// <summary>The JSON text of <paramref name="node"/>; <paramref name="indent"/> spaces per level (0: one line).</summary>
    public static string Stringify(JsonNode? node, int indent = 0)
    {
        var builder = new StringBuilder();
        Write(builder, node, indent, 0);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, JsonNode? node, int indent, int depth)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject obj:
                WriteList(builder, obj.Select(p => ((string?)p.Key, p.Value)).ToList(), '{', '}', indent, depth, isObject: true);
                break;
            case JsonArray array:
                WriteList(builder, array.Select(v => ((string?)null, v)).ToList(), '[', ']', indent, depth, isObject: false);
                break;
            case JsonValue value:
                WriteValue(builder, value);
                break;
        }
    }

    private static void WriteList(StringBuilder builder, List<(string? Key, JsonNode? Value)> items, char open, char close, int indent, int depth, bool isObject)
    {
        if (items.Count == 0)
        {
            builder.Append(open).Append(close);
            return;
        }
        builder.Append(open);
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }
            if (indent > 0)
            {
                builder.Append('\n').Append(' ', indent * (depth + 1));
            }
            if (isObject)
            {
                WriteString(builder, items[i].Key!);
                builder.Append(indent > 0 ? ": " : ":");
            }
            Write(builder, items[i].Value, indent, depth + 1);
        }
        if (indent > 0)
        {
            builder.Append('\n').Append(' ', indent * depth);
        }
        builder.Append(close);
    }

    private static void WriteValue(StringBuilder builder, JsonValue value)
    {
        if (value.TryGetValue<string>(out var text))
        {
            WriteString(builder, text);
        }
        else if (value.TryGetValue<bool>(out var flag))
        {
            builder.Append(flag ? "true" : "false");
        }
        else if (value.TryGetValue<double>(out var number))
        {
            builder.Append(Number(number));
        }
        else
        {
            builder.Append(value.ToJsonString());
        }
    }

    /// <summary>
    /// JavaScript's <c>x.toFixed(digits)</c> for 0 ≤ x &lt; 1e21: the exact value of the double, ties to
    /// the larger number (1.25 gives "1.3", 1.15 gives "1.1"), not .NET's "F" rounding.
    /// </summary>
    public static string ToFixed(double x, int digits)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x < 0 || x >= 1e21)
        {
            return x.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
        var bits = BitConverter.DoubleToInt64Bits(x);
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0)
        {
            exponent = 1;
        }
        else
        {
            mantissa |= 1L << 52;
        }
        exponent -= 1075;
        var numerator = new System.Numerics.BigInteger(mantissa) * System.Numerics.BigInteger.Pow(10, digits);
        var denominator = System.Numerics.BigInteger.One;
        if (exponent > 0)
        {
            numerator <<= exponent;
        }
        else
        {
            denominator <<= -exponent;
        }
        var whole = System.Numerics.BigInteger.DivRem(numerator, denominator, out var remainder);
        if (remainder * 2 >= denominator)
        {
            whole += 1;
        }
        var text = whole.ToString(CultureInfo.InvariantCulture).PadLeft(digits + 1, '0');
        return digits == 0 ? text : text[..^digits] + "." + text[^digits..];
    }

    /// <summary>A number as JavaScript prints it.</summary>
    public static string Number(double number)
    {
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            return "null";
        }
        if (number == Math.Floor(number) && Math.Abs(number) < 1e21)
        {
            return number.ToString("0", CultureInfo.InvariantCulture);
        }
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        return text.Replace("E+", "e+", StringComparison.Ordinal).Replace("E-", "e-", StringComparison.Ordinal);
    }

    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append(@"\\"); break;
                case '\b': builder.Append(@"\b"); break;
                case '\f': builder.Append(@"\f"); break;
                case '\n': builder.Append(@"\n"); break;
                case '\r': builder.Append(@"\r"); break;
                case '\t': builder.Append(@"\t"); break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append(@"\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }
        builder.Append('"');
    }
}
