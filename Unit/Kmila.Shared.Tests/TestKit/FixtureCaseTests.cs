using Kmila.Frontend;
using Kmila.Shared.Services.Vcd;
using Kmila.Shared.Tests.Support;
using Kmila.TestKit.Cases;
using Kmila.TestKit.Runners;

namespace Kmila.Shared.Tests.TestKit;

/// <summary>
/// v3.0.10: every fixture case, in every supported language, simulated by Kmila from
/// its sources verbatim and compared (nine values, t=0 included) against the
/// committed reference golden <c>variants/&lt;lang&gt;/expected.vcd</c> — so these
/// tests need no reference simulator. Regenerate goldens with
/// <c>SimulatorBench fixtures</c>.
/// </summary>
public class FixtureCaseTests
{
    private static readonly HdlLanguage Fake = new("fakehdl", "FakeHDL", [".fk"], "//", "fakehdl", true);

    public static TheoryData<string, string> Cases() => HdlVariantCases.All();

    [Fact]
    public void EverySupportedLanguageGetsARowAndAMissingOneIsReported()
    {
        int cases = CaseLoader.FindAll(HdlVariantCases.Root).Count();
        Assert.True(cases > 0);
        using (HdlLanguages.RegisterForTesting(Fake))
        {
            var rows = HdlVariantCases.All().Select(r => ((string)r[0], (string)r[1])).ToList();
            Assert.Equal(cases * HdlLanguages.Supported.Count, rows.Count);
            Assert.All(rows.Where(r => r.Item2 == Fake.Id), r =>
                Assert.Null(CaseLoader.Load(Path.Combine(HdlVariantCases.Root, r.Item1)).Variants.For(Fake)));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task KmilaMatchesTheReferenceGolden(string caseDir, string languageId)
    {
        var c = CaseLoader.Load(Path.Combine(HdlVariantCases.Root, caseDir));
        var language = HdlLanguages.FromId(languageId)!;
        var variant = c.Variants.For(language);
        Assert.True(variant != null, $"{caseDir}: missing {language.DisplayName} variant");

        var golden = VcdReader.ParseFile(variant!.ExpectedVcdPath);
        var (response, diff) = await CaseSimulation.RunAsync(new InProcessKmilaRunner(), c.Spec, variant, CaseSimulation.Read(variant.SourcePaths), golden);
        Assert.Null(response.Error);
        Assert.All(diff, d => Assert.True(d.Equal, $"{caseDir} [{languageId}] {d.Describe("golden", "kmila")}"));
    }
}
