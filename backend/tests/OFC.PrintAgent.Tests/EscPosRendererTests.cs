using System.Text;
using OFC.PrintAgent;
using Xunit;

namespace OFC.PrintAgent.Tests;

public class EscPosRendererTests
{
    [Fact]
    public void CashDrawerOpen_job_is_only_the_pulse_sequence()
    {
        var bytes = EscPosRenderer.Render("CashDrawerOpen", null, 42, "{}");
        Assert.Equal([0x1B, 0x70, 0x00, 0x19, 0xFA], bytes);
    }

    [Fact]
    public void Receipt_job_starts_with_init_ends_with_cut_and_includes_drawer_kick()
    {
        var bytes = EscPosRenderer.Render("Receipt", null, 42, """{"orderNumber":"A1"}""");
        Assert.Equal([0x1B, 0x40], bytes[..2]);
        Assert.Equal([0x1D, 0x56, 0x00], bytes[^3..]);
        var kick = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA };
        Assert.Contains(Chunk(bytes, kick.Length), chunk => chunk.SequenceEqual(kick));
    }

    [Fact]
    public void Kitchen_job_does_not_kick_the_drawer()
    {
        var withoutTemplate = EscPosRenderer.Render("Kitchen", null, 42, """{"orderNumber":"A1"}""");
        // ESC p 0 25 250 (the drawer-kick sequence) must not appear as a contiguous run.
        var kick = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA };
        Assert.DoesNotContain(Chunk(withoutTemplate, kick.Length), chunk => chunk.SequenceEqual(kick));
    }

    [Fact]
    public void Substitute_resolves_top_level_and_nested_fields()
    {
        var template = "Order {{orderNumber}} / Station {{station.code}}";
        var result = EscPosRenderer.Substitute(template, """{"orderNumber":"A1","station":{"code":"GRILL"}}""");
        Assert.Equal("Order A1 / Station GRILL", result);
    }

    [Fact]
    public void Substitute_replaces_missing_fields_with_empty_string()
    {
        var result = EscPosRenderer.Substitute("Note: {{missing}}", "{}");
        Assert.Equal("Note: ", result);
    }

    [Fact]
    public void Render_wraps_long_lines_to_the_template_width()
    {
        // Below the 20-char clamp floor, Render treats the width as a misconfiguration and holds the
        // line at 20 — this uses 24 so the test exercises real wrapping, not the clamp guard.
        var payload = """{"line":"one two three four five six seven eight nine ten eleven twelve"}""";
        var bytes = EscPosRenderer.Render("Kitchen", "{{line}}", 24, payload);
        // Skip the 2-byte ESC @ init header — it's a control sequence for the printer, not text.
        var text = Encoding.ASCII.GetString(bytes, 2, bytes.Length - 2);
        foreach (var line in text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            Assert.True(line.Length <= 24, $"Line '{line}' exceeds width 24.");
    }

    private static IEnumerable<byte[]> Chunk(byte[] source, int length)
    {
        for (var i = 0; i <= source.Length - length; i++) yield return source[i..(i + length)];
    }
}
