using Kmila.Frontend;

namespace Kmila.Shared.Tests.Content;

/// <summary><see cref="HdlVariants{T}"/> resolves content variants against the language registry.</summary>
public class HdlVariantsTests
{
    private static readonly HdlLanguage Fake = new("fakehdl", "FakeHDL", [".fk"], "//", "fakehdl", true);

    [Fact]
    public void Unknown_language_ids_are_ignored_and_ids_match_case_insensitively()
    {
        var v = HdlVariants<string>.FromIds(new Dictionary<string, string> { ["VHDL"] = "a", ["systemverilog"] = "b" });
        Assert.Equal([HdlLanguages.Vhdl], v.Languages);
        Assert.Equal("a", v.For(HdlLanguages.Vhdl));
        Assert.Equal(HdlLanguages.Vhdl, v.Preferred!.Value.Language);
    }

    [Fact]
    public void Empty_set_has_no_preferred_variant()
    {
        var v = HdlVariants<string>.FromIds(null);
        Assert.True(v.IsEmpty);
        Assert.Null(v.Preferred);
    }

    [Fact]
    public void A_newly_supported_language_without_a_variant_is_reported_missing()
    {
        var v = HdlVariants<string>.FromIds(new Dictionary<string, string> { ["vhdl"] = "a" });
        using (HdlLanguages.RegisterForTesting(Fake))
            Assert.Equal([Fake], v.MissingFrom(HdlLanguages.Supported));
        Assert.Empty(v.MissingFrom(HdlLanguages.Supported));
    }
}
