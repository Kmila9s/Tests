using System.Reflection;
using Kmila.Shared.Pages;
using Kmila.Shared.Services.VisualRun;

namespace Kmila.Shared.Tests;

// 2026-11: with Advanced mode OFF the 7-seg bank is wired pin-by-pin (packed
// vector wiring is Advanced-only). The shipped samples were rewired from
// packed to explicit; these tests prove the explicit bindings pick exactly
// the bits the old packed layout fed the widget, for every bus value.
public class SevenSegBankExplicitWiringTests
{
    private static KvizDoc LoadSample(string constName)
    {
        var f = typeof(ProjectsView).GetField(constName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(constName);
        var doc = KvizSerializer.Deserialize((string)f.GetRawConstantValue()!);
        Assert.NotNull(doc);
        return doc!;
    }

    private static string Bits(int value, int width)
        => Convert.ToString(value, 2).PadLeft(width, '0');

    private static string Read(KvizComponent c, string pin, Dictionary<string, string> ports)
    {
        var (name, slice) = SliceHelper.Parse(c.Bindings[pin]);
        return SliceHelper.Extract(ports[name], slice);
    }

    [Fact]
    public void Counter9999_explicitMuxWiring_matchesOldPackedSegSel()
    {
        var bank = LoadSample("CONTADOR9999_KVIZ").Components.Single(c => c.Type == "seven-seg-bank");
        Assert.Equal("explicit", KvizWidgetCatalog.EffectiveWiring(bank, advanced: false));
        // Every pin the raw surface exposes is wired.
        foreach (var p in KvizWidgetCatalog.PinsOf(bank, advanced: false))
            Assert.True(bank.Bindings.ContainsKey(p.Name), p.Name);

        for (int seg = 0; seg < 128; seg++)
        for (int sel = 0; sel < 16; sel++)
        {
            var ports = new Dictionary<string, string> { ["segmentos"] = Bits(seg, 7), ["anodos"] = Bits(sel, 4) };
            // Old packed: seg = segmentos (char0 = a); sel char (N-1-d) = digit d.
            string explicitSeg = string.Concat("abcdefg".Select(s => Read(bank, s.ToString(), ports)[^1]));
            Assert.Equal(ports["segmentos"], explicitSeg);
            for (int d = 0; d < 4; d++)
                Assert.Equal(ports["anodos"][3 - d], Read(bank, $"an{d}", ports)[^1]);
        }
    }

    [Fact]
    public void Capstone_explicitRawWiring_matchesOldPackedValueBus()
    {
        var bank = LoadSample("CAPSTONE_CALC_KVIZ").Components.Single(c => c.Type == "seven-seg-bank");
        foreach (var p in KvizWidgetCatalog.PinsOf(bank, advanced: false))
            Assert.True(bank.Bindings.ContainsKey(p.Name), p.Name);

        var rng = new Random(1234);
        for (int iter = 0; iter < 2000; iter++)
        {
            var bus = new string(Enumerable.Range(0, 28).Select(_ => rng.Next(2) == 0 ? '0' : '1').ToArray());
            var ports = new Dictionary<string, string> { ["seg_bus"] = bus };
            for (int d = 0; d < 4; d++)
            {
                // Old packed: digit d = 7-char slice at offset (3-d)*7, char0 = a.
                string packed = bus.Substring((3 - d) * 7, 7);
                string explicitDigit = string.Concat("abcdefg".Select(s =>
                    Read(bank, KvizWidgetCatalog.BankDigitPin(d, s.ToString()), ports)[^1]));
                Assert.Equal(packed, explicitDigit);
            }
        }
    }

    [Fact]
    public void BankRaw_advancedOff_exposesOnePinPerSegmentPerDigit()
    {
        var c = new KvizComponent { Type = "seven-seg-bank" };
        c.Config["digits"] = System.Text.Json.JsonSerializer.SerializeToElement(8);
        var pins = KvizWidgetCatalog.PinsOf(c, advanced: false).Select(p => p.Name).ToList();
        Assert.Equal(56, pins.Count);
        Assert.Contains("d7_g", pins);
        Assert.DoesNotContain("value", pins);
        // Advanced ON keeps the legacy packed default.
        Assert.Equal(new[] { "value" }, KvizWidgetCatalog.PinsOf(c, advanced: true).Select(p => p.Name));
    }

    [Fact]
    public void BankMux_advancedOff_isExplicit_evenIfFileSaysPacked()
    {
        var c = new KvizComponent { Type = "seven-seg-bank" };
        c.Config["mode"] = System.Text.Json.JsonSerializer.SerializeToElement("mux");
        c.Config["wiring"] = System.Text.Json.JsonSerializer.SerializeToElement("packed");
        var off = KvizWidgetCatalog.PinsOf(c, advanced: false).Select(p => p.Name).ToHashSet();
        Assert.Contains("a", off);
        Assert.Contains("an3", off);
        Assert.DoesNotContain("seg", off);
        Assert.Contains("seg", KvizWidgetCatalog.PinsOf(c, advanced: true).Select(p => p.Name));
    }
}
