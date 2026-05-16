using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnvifManager.Cli.Output;

internal static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void Write(object? payload, bool json)
    {
        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(payload, JsonOpts));
            return;
        }
        WriteText(payload, indent: 0);
    }

    public static void WriteError(string message, int code, bool json)
    {
        if (json)
        {
            var payload = new { error = message, code };
            Console.Error.WriteLine(JsonSerializer.Serialize(payload, JsonOpts));
        }
        else
        {
            Console.Error.WriteLine($"error: {message}");
        }
    }

    private static void WriteText(object? value, int indent)
    {
        var pad = new string(' ', indent);

        switch (value)
        {
            case null:
                Console.Out.WriteLine($"{pad}(null)");
                return;
            case string s:
                Console.Out.WriteLine($"{pad}{s}");
                return;
            case bool b:
                Console.Out.WriteLine($"{pad}{(b ? "true" : "false")}");
                return;
        }

        var t = value.GetType();

        if (t.IsPrimitive || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan) || t.IsEnum)
        {
            Console.Out.WriteLine($"{pad}{value}");
            return;
        }

        if (value is IEnumerable enumerable)
        {
            var first = true;
            foreach (var item in enumerable)
            {
                if (!first) Console.Out.WriteLine();
                WriteText(item, indent);
                first = false;
            }
            if (first) Console.Out.WriteLine($"{pad}(empty)");
            return;
        }

        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            object? v;
            try { v = prop.GetValue(value); }
            catch { continue; }

            var label = $"{pad}{prop.Name}: ";
            if (v == null)
            {
                Console.Out.WriteLine($"{label}");
                continue;
            }
            var vt = v.GetType();
            if (v is string || vt.IsPrimitive || vt == typeof(decimal) || vt == typeof(DateTime) || vt == typeof(TimeSpan) || vt.IsEnum)
            {
                Console.Out.WriteLine($"{label}{v}");
            }
            else if (v is IEnumerable list)
            {
                var items = list.Cast<object?>().ToList();
                if (items.Count == 0)
                {
                    Console.Out.WriteLine($"{label}(empty)");
                }
                else
                {
                    Console.Out.WriteLine($"{label}");
                    foreach (var item in items)
                    {
                        WriteText(item, indent + 2);
                    }
                }
            }
            else
            {
                Console.Out.WriteLine($"{label}");
                WriteText(v, indent + 2);
            }
        }
    }
}
