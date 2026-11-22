using Kmila.Frontend;
using Kmila.Shared.Tests.Support;

namespace Kmila.Shared.Tests.Content;

/// <summary>
/// v3.0.11 (Fase 0, 5f): every multi-variant content item has a variant for each
/// language it requires — and the check really bites: registering a new language
/// makes every item that requires all languages report it as missing. Adding
/// SystemVerilog therefore lists exactly the content still to write.
/// </summary>
public class LanguageCoverageTests
{
    private static readonly HdlLanguage Fake = new("fakehdl", "FakeHDL", [".fk"], "//", "fakehdl", true);

    [Fact]
    public async Task EveryItemHasEveryRequiredVariant()
    {
        var items = await ContentCoverage.AllAsync();
        Assert.All(Enum.GetValues<ContentKind>(), kind => Assert.Contains(items, i => i.Kind == kind));
        var missing = items.SelectMany(i => i.Missing().Select(l => $"{i.Label}: {l.Id}")).ToList();
        Assert.True(missing.Count == 0, "missing variants:\n" + string.Join("\n", missing));
    }

    [Fact]
    public async Task ANewLanguageIsReportedMissingEverywhereItIsRequired()
    {
        var items = await ContentCoverage.AllAsync();
        using (HdlLanguages.RegisterForTesting(Fake))
        {
            var lacking = items.Where(i => i.Missing().Contains(Fake)).ToList();
            // Practice, templates and fixtures require every language; Learn only its neutral modules.
            Assert.All(Enum.GetValues<ContentKind>(), kind => Assert.Contains(lacking, i => i.Kind == kind));
            Assert.All(items.Where(i => i.Kind != ContentKind.Learn), i => Assert.Contains(i, lacking));
        }
    }
}
