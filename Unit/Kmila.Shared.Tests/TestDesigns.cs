using Interpreter.Services;
using Kmila.Frontend;

namespace Kmila.Shared.Tests;

/// <summary>
/// Single entry point for tests that need a parsed design. Goes through the
/// production HDL front-end (clean registries → scan → parse), so tests
/// exercise exactly what the app runs instead of a hand-copied pipeline.
/// </summary>
internal static class TestDesigns
{
    public static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    public static string Fixture(params string[] parts) => File.ReadAllText(FixturePath(parts));

    /// <summary>
    /// Loads <paramref name="vhdl"/> as a one-file project and returns the
    /// top entity (<paramref name="entry"/> when it has an architecture, else
    /// the first entity that has one). Throws the original parse exception.
    /// </summary>
    public static Entity LoadTop(string vhdl, string? entry = null)
    {
        var design = Load(vhdl, entry);
        design.ThrowIfFailed();
        return design.Top ?? throw new Xunit.Sdk.XunitException("No entity with an architecture was parsed.");
    }

    /// <summary>Loads <paramref name="vhdl"/> as a one-file project through the VHDL front-end.</summary>
    public static DesignResult Load(string vhdl, string? entry = null)
    {
        // Per-run engine attribute state is not part of the design registries.
        SignalAttributeHandler.Instance.Reset();
        var dir = Path.Combine(Path.GetTempPath(), "kmila-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, (entry ?? "design") + HdlLanguages.Vhdl.PrimaryExtension), vhdl);
            return Frontends.For(HdlLanguages.Vhdl).LoadDesign(new DesignRequest { Root = dir, Entry = entry });
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>The free-running counter used by the long-run / watchdog / stress suites.</summary>
    public static Entity SelfRetriggeringClock() => LoadTop(Fixture("Stress", "self_retriggering_clock.vhd"));

    /// <summary>Parses <paramref name="vhdl"/> without touching registries; throws the original parse exception.</summary>
    public static ParsedUnit Parse(string vhdl)
    {
        var unit = Frontends.For(HdlLanguages.Vhdl).ParseSource(vhdl);
        unit.ThrowIfFailed();
        return unit;
    }
}
