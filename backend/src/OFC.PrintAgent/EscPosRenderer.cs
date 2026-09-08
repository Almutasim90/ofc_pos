using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OFC.PrintAgent;

// Renders a print job into raw ESC/POS bytes. A PrintTemplate.Content is plain text with
// "{{field}}" / "{{field.nested}}" placeholders resolved against the job's JSON payload; a job with
// no template (or a CashDrawerOpen job, which has no receipt body) falls back to the drawer-kick pulse
// alone or a minimal dump of the payload, so a misconfigured route still produces something readable
// rather than silently losing the print.
public static partial class EscPosRenderer
{
    private static readonly byte[] Init = [0x1B, 0x40]; // ESC @ : initialize printer
    private static readonly byte[] CutFull = [0x1D, 0x56, 0x00]; // GS V 0 : full cut
    // ESC p 0 25 250 : pulse drawer-kick pin 2, ~25*2ms on, ~250*2ms off — the standard sequence
    // supported by essentially every printer with an RJ11 cash-drawer port.
    private static readonly byte[] DrawerKick = [0x1B, 0x70, 0x00, 0x19, 0xFA];

    public static byte[] Render(string kind, string? templateContent, int widthChars, string payloadJson)
    {
        if (kind == "CashDrawerOpen") return DrawerKick;

        var body = templateContent is null
            ? FallbackText(payloadJson)
            : Substitute(templateContent, payloadJson);
        var wrapped = Wrap(body, Math.Clamp(widthChars, 20, 120));
        var text = Encoding.ASCII.GetBytes(wrapped.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n\r\n\r\n");

        var bytes = new List<byte>(Init.Length + text.Length + DrawerKick.Length + CutFull.Length);
        bytes.AddRange(Init);
        bytes.AddRange(text);
        // Receipt printers conventionally kick the drawer on the receipt job itself (cash sales open
        // the drawer when the receipt prints), not only on a dedicated CashDrawerOpen job.
        if (kind == "Receipt") bytes.AddRange(DrawerKick);
        bytes.AddRange(CutFull);
        return bytes.ToArray();
    }

    internal static string Substitute(string template, string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        return PlaceholderPattern().Replace(template, match =>
        {
            var path = match.Groups[1].Value.Trim();
            var current = doc.RootElement;
            foreach (var segment in path.Split('.'))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next)) return string.Empty;
                current = next;
            }
            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString() ?? "",
                JsonValueKind.Number => current.GetRawText(),
                JsonValueKind.True or JsonValueKind.False => current.GetRawText(),
                JsonValueKind.Array => string.Join(", ", current.EnumerateArray().Select(x => x.ToString())),
                _ => ""
            };
        });
    }

    private static string FallbackText(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var lines = new List<string>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            lines.Add($"{prop.Name}: {(prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString())}");
        return string.Join('\n', lines);
    }

    private static string Wrap(string text, int width)
    {
        var result = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            var remaining = line;
            while (remaining.Length > width)
            {
                var breakAt = remaining.LastIndexOf(' ', Math.Min(width, remaining.Length - 1));
                if (breakAt <= 0) breakAt = width;
                result.Append(remaining[..breakAt].TrimEnd()).Append('\n');
                remaining = remaining[breakAt..].TrimStart();
            }
            result.Append(remaining).Append('\n');
        }
        return result.ToString().TrimEnd('\n');
    }

    [GeneratedRegex(@"\{\{\s*([\w.]+)\s*\}\}")]
    private static partial Regex PlaceholderPattern();
}
