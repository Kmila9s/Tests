using System.Text.Json;
using Kmila.Shared.Services.VisualRun;

namespace Kmila.Shared.Tests;

// 2026-11: Advanced mode is enforced, not just hidden in the property pane.
// With it OFF, a widget whose .kviz stores an assisted "Expert" mode (7-seg
// hex/dec, LCD text, keypad decoded, …) or no mode at all (legacy files whose
// default was Expert) is shown, wired and run in its raw pin-level mode. The
// stored config is never rewritten, and Sanitize must not delete bindings.
public class AdvancedModeEnforcementTests
{
    private static KvizComponent Comp(string type, string? mode, params (string pin, string port)[] bindings)
    {
        var c = new KvizComponent { Id = "w", Type = type };
        if (mode != null) c.Config["mode"] = JsonSerializer.SerializeToElement(mode);
        foreach (var (pin, port) in bindings) c.Bindings[pin] = port;
        return c;
    }

    [Theory]
    [InlineData("seven-seg", "hex", "raw")]
    [InlineData("seven-seg", "dec", "raw")]
    [InlineData("seven-seg-bank", "hex", "raw")]
    [InlineData("seven-seg-bank", "dec", "raw")]
    [InlineData("lcd-hd44780", "text", "raw-4bit")]
    [InlineData("keypad-matrix", "decoded", "raw-scan")]
    [InlineData("buzzer-piezo", "freq", "signal")]
    [InlineData("matrix-3x3", "value", "raw")]
    [InlineData("matrix-3x3-rgb", "planes", "raw")]
    [InlineData("multi-switch", "vector", "per-bit")]
    public void ExpertMode_collapsesToRaw_whenAdvancedOff(string type, string stored, string expectedRaw)
    {
        var c = Comp(type, stored);
        Assert.Equal(expectedRaw, KvizWidgetCatalog.EffectiveMode(c, advanced: false));
        Assert.Equal(stored, KvizWidgetCatalog.EffectiveMode(c, advanced: true));
        // Stored config untouched.
        Assert.Equal(stored, c.Config["mode"].GetString());
    }

    [Theory]
    [InlineData("lcd-hd44780", "raw-4bit")]
    [InlineData("keypad-matrix", "raw-scan")]
    [InlineData("buzzer-piezo", "signal")]
    [InlineData("matrix-3x3", "raw")]
    [InlineData("matrix-3x3-rgb", "raw")]
    public void MissingMode_legacyExpertDefault_isRawWhenAdvancedOff(string type, string expectedRaw)
    {
        var c = Comp(type, null);
        Assert.Equal(expectedRaw, KvizWidgetCatalog.EffectiveMode(c, advanced: false));
    }

    [Theory]
    [InlineData("seven-seg-bank", "mux")]
    [InlineData("lcd-hd44780", "raw-8bit")]
    [InlineData("lcd-hd44780", "raw-panel")]
    [InlineData("seven-seg", "raw")]
    public void RawModes_arePreserved_whenAdvancedOff(string type, string stored)
        => Assert.Equal(stored, KvizWidgetCatalog.EffectiveMode(Comp(type, stored), advanced: false));

    [Fact]
    public void SevenSegHex_pinsAreRawSegments_whenAdvancedOff()
    {
        var c = Comp("seven-seg", "hex");
        var off = KvizWidgetCatalog.PinsOf(c, advanced: false).Select(p => p.Name).ToHashSet();
        var on = KvizWidgetCatalog.PinsOf(c, advanced: true).Select(p => p.Name).ToHashSet();
        Assert.Contains("a", off);
        Assert.DoesNotContain("value", off);
        Assert.Contains("value", on);
        Assert.DoesNotContain("a", on);
    }

    [Fact]
    public void Sanitize_advancedOff_keepsBothExpertAndRawBindings()
    {
        var c = Comp("seven-seg", "hex", ("value", "num"), ("a", "seg[6]"), ("bogus", "x"));
        var doc = new KvizDoc { Components = { c } };
        KvizWidgetCatalog.Sanitize(doc, advanced: false);
        Assert.True(c.Bindings.ContainsKey("value"));
        Assert.True(c.Bindings.ContainsKey("a"));
        Assert.False(c.Bindings.ContainsKey("bogus"));
    }

    [Fact]
    public void Sanitize_advancedOn_isUnchanged_dropsOtherModePins()
    {
        var c = Comp("seven-seg", "hex", ("value", "num"), ("a", "seg[6]"));
        var doc = new KvizDoc { Components = { c } };
        KvizWidgetCatalog.Sanitize(doc);
        Assert.True(c.Bindings.ContainsKey("value"));
        Assert.False(c.Bindings.ContainsKey("a"));
    }
}
