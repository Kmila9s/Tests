using BlazorBootstrap;
using Kmila.Shared.Services;
using Kmila.Shared.Services.Api;

namespace Kmila.Shared.Tests;

/// <summary>
/// The app derives file presentation and API language handling from the HDL
/// language registry instead of inline extension checks (v3.0.3).
/// </summary>
public class HdlLanguageWiringTests
{
    [Theory]
    [InlineData("main.vhd", "vhdl", "VHD", "vhdl")]
    [InlineData("main.VHDL", "vhdl", "VHDL", "vhdl")]
    [InlineData("notes.md", "markdown", "MD", "markdown")]
    [InlineData("data.bin", "other", "BIN", "plaintext")]
    [InlineData("Makefile", "other", "", "plaintext")]
    public void FileTypes_resolves_variant_label_and_monaco_language(string file, string variant, string label, string monaco)
    {
        var kind = FileTypes.Of(file);
        Assert.Equal(variant, kind.Variant);
        Assert.Equal(label, kind.Label);
        Assert.Equal(monaco, kind.MonacoLanguage);
    }

    [Fact]
    public void FileTypes_gives_every_hdl_source_the_same_icon()
    {
        Assert.Equal(IconName.FileCode, FileTypes.Of("a.vhd").Icon);
        Assert.Equal(FileTypes.Of("a.vhd").Icon, FileTypes.Of("b.vhdl").Icon);
    }

    [Fact]
    public async Task Simulate_rejects_an_unknown_language_id()
    {
        var store = new SandboxProjectStore();
        var project = store.Create("lang-test", retain: false);
        try
        {
            project.WriteFile("and2.vhd", "entity and2 is port (a, b : in bit; y : out bit); end and2;\n" +
                                          "architecture rtl of and2 is begin y <= a and b; end rtl;");
            var sim = new HeadlessSimulator(store);
            var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
                sim.RunAsync(new SimulateRequest { ProjectId = project.Id, Language = "cobol" }));
            Assert.Contains("cobol", ex.Message);
        }
        finally { store.Delete(project.Id); }
    }
}
