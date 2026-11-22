using Kmila.Frontend;
using Kmila.Shared.Services.Vcd;
using Kmila.TestKit.Cases;

namespace Kmila.Shared.Tests.TestKit;

/// <summary>
/// v3.0.8: the multi-variant case model (case.json + variants/&lt;lang&gt;/variant.json),
/// the per-case verdict and the known-gaps ratchet.
/// </summary>
public class CaseModelTests
{
    private static readonly HdlLanguage Fake = new("fakehdl", "FakeHDL", [".fk"], "//", "fakehdl", true);

    private const string Golden = "$timescale 1 fs $end $scope module tb $end $var reg 1 ! y $end $upscope $end $enddefinitions $end #0 0! #5000000 1!";

    private static HdlVariants<VariantOutcome> Outcomes(params (string Id, VariantOutcome Outcome)[] items) =>
        HdlVariants<VariantOutcome>.FromIds(items.ToDictionary(i => i.Id, i => i.Outcome));

    [Fact]
    public void Loader_ReadsTheSpecAndEachVariant()
    {
        string dir = Directory.CreateTempSubdirectory("kmila-case").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "case.json"), """
                { "version": 2, "id": "001-and", "title": "AND", "ports": [ { "name": "a", "dir": "in", "width": 1 }, { "name": "y", "dir": "out", "width": 1 } ],
                  "durationNs": 40, "stimulus": [ { "t": 0, "port": "a", "value": "1" } ] }
                """);
            Directory.CreateDirectory(Path.Combine(dir, "variants", "vhdl"));
            File.WriteAllText(Path.Combine(dir, "variants", "vhdl", "variant.json"), """
                { "language": "vhdl", "sources": [ "and.vhdl", "tb.vhdl" ], "top": "tb", "entity": "and_gate", "standard": "93c" }
                """);
            var c = CaseLoader.Load(dir);
            Assert.Equal("001-and", c.Spec.Id);
            Assert.Equal(["y"], c.Spec.Outputs);
            Assert.Equal("a", c.Spec.Inputs.Single().Name);
            Assert.Equal(("1", 0), (c.Spec.Stimulus[0].Value, c.Spec.Stimulus[0].Time));
            var v = c.Variants.For(HdlLanguages.Vhdl)!;
            Assert.Same(HdlLanguages.Vhdl, v.Language);
            Assert.Equal(Path.Combine(dir, "variants", "vhdl", "reference", "tb.vhdl"), v.SourcePaths[1]);

            // A language that is not enabled yet is ignored (it may ship before its front-end) …
            File.WriteAllText(Path.Combine(dir, "variants", "vhdl", "variant.json"), """{ "language": "cobol" }""");
            Assert.True(CaseLoader.Load(dir).Variants.IsEmpty);
            // … but another schema version is never half-read.
            File.WriteAllText(Path.Combine(dir, "case.json"), """{ "id": "001-and" }""");
            Assert.Throws<InvalidDataException>(() => CaseLoader.Load(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Verdict_PassesOnlyWithEveryVariantPassing()
    {
        var golden = VcdReader.Parse(Golden);
        var pass = Outcomes(("vhdl", new(true, golden)));
        Assert.Equal(CaseOutcome.Pass, VariantVerdict.Aggregate(HdlLanguages.Supported, pass, ["y"]).Outcome);

        var fail = Outcomes(("vhdl", new(false, golden)));
        Assert.Equal(CaseOutcome.Fail, VariantVerdict.Aggregate(HdlLanguages.Supported, fail, ["y"]).Outcome);
    }

    [Fact]
    public void Verdict_MissingVariantFailsTheCase()
    {
        var pass = Outcomes(("vhdl", new(true, VcdReader.Parse(Golden))));
        using (HdlLanguages.RegisterForTesting(Fake))
        {
            var v = VariantVerdict.Aggregate(HdlLanguages.Supported, pass, ["y"]);
            Assert.Equal(CaseOutcome.MissingVariant, v.Outcome);
            Assert.Equal([Fake], v.Missing);
        }
    }

    [Fact]
    public void Verdict_DisagreeingGoldensFailTheCase()
    {
        var other = VcdReader.Parse(Golden.Replace("#5000000 1!", "#5000000 0!"));
        using (HdlLanguages.RegisterForTesting(Fake))
        {
            var both = Outcomes(("vhdl", new(true, VcdReader.Parse(Golden))), ("fakehdl", new(true, other)));
            var v = VariantVerdict.Aggregate(HdlLanguages.Supported, both, ["y"]);
            Assert.Equal(CaseOutcome.Fail, v.Outcome);
            Assert.False(v.GoldensAgree);
        }
    }

    [Fact]
    public void KnownGaps_RatchetCatchesNewFailuresAndFixedGaps()
    {
        var listed = new List<KnownGap> { new() { Case = "009", Variant = "vhdl", Category = "semantics", Reason = "G84" } };
        var ok = KnownGaps.Apply(listed, [("009", "vhdl", false), ("010", "vhdl", true)]);
        Assert.Empty(ok.Violations);
        Assert.Equal("G84", ok.ReasonsByCase["009"]);
        Assert.Equal(["010/vhdl: fails and is not in known-gaps.json"], KnownGaps.Apply(listed, [("009", "vhdl", false), ("010", "vhdl", false)]).Violations);
        Assert.Equal(["009/vhdl: passes but is still in known-gaps.json (remove it)"], KnownGaps.Apply(listed, [("009", "vhdl", true)]).Violations);
    }

    [Fact]
    public void KnownGaps_LoadFromTheFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"known-gaps-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """[ { "case": "057-barrel", "variant": "vhdl", "category": "semantics", "reason": "G85", "since": "2026-11-21" } ]""");
            var back = KnownGaps.Load(path).Single();
            Assert.Equal(("057-barrel", "vhdl", "semantics", "G85"), (back.Case, back.Variant, back.Category, back.Reason));
            Assert.Empty(KnownGaps.Load(path + ".missing"));
        }
        finally { File.Delete(path); }
    }
}
