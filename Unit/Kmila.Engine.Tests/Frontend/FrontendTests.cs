using Interpreter.Services;
using Kmila.Frontend;

namespace Kmila.Engine.Tests.Frontend;

/// <summary>Contract tests for the language-neutral HDL front-end (Kmila.Frontend).</summary>
public sealed class FrontendTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kmila-fe-" + Guid.NewGuid().ToString("N"));

    public FrontendTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private const string AndGate = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity and2 is
          port (a, b : in std_logic; y : out std_logic);
        end entity;
        architecture rtl of and2 is
        begin
          y <= a and b;
        end architecture;
        """;

    private const string StructuralTop = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity top is
          port (x, z : in std_logic; q : out std_logic);
        end top;
        architecture structural of top is
          component and2 port (a, b : in std_logic; y : out std_logic); end component;
        begin
          u0 : and2 port map (a => x, b => z, y => q);
        end structural;
        """;

    [Theory]
    [InlineData("design.vhd", true)]
    [InlineData("DESIGN.VHDL", true)]
    [InlineData("notes.md", false)]
    [InlineData("_elaborated_top.vhd", false)]
    public void Registry_classifies_sources(string file, bool isSource) =>
        Assert.Equal(isSource, HdlLanguages.IsHdlSource(file));

    [Fact]
    public void Registry_resolves_language_by_path_and_id()
    {
        Assert.Same(HdlLanguages.Vhdl, HdlLanguages.FromPath("a/b/c.vhdl"));
        Assert.Same(HdlLanguages.Vhdl, HdlLanguages.FromId("VHDL"));
        Assert.Null(HdlLanguages.FromPath("c.txt"));
        Assert.Equal(".vhd,.vhdl", HdlLanguages.AcceptList());
    }

    [Fact]
    public void ForSources_rejects_when_no_hdl_source()
    {
        Assert.Throws<NotSupportedException>(() => Frontends.ForSources(["readme.md", "x.kviz"]));
    }

    [Fact]
    public void LoadDesign_picks_entry_file_and_top()
    {
        Write("and2.vhd", AndGate);
        Write("top.vhd", StructuralTop);
        var design = Frontends.For(HdlLanguages.Vhdl).LoadDesign(new DesignRequest { Root = _dir, Entry = "top" });

        Assert.Null(design.Error);
        Assert.Equal("top", design.Top?.Name);
        Assert.EndsWith("top.vhd", design.SourcePath);
        Assert.Equal(2, design.Sources.Count);
    }

    [Fact]
    public void LoadDesign_elaborates_in_memory_without_writing_into_the_project()
    {
        Write("and2.vhd", AndGate);
        Write("top.vhd", StructuralTop);
        var before = Directory.GetFiles(_dir, "*", SearchOption.AllDirectories).Length;

        var design = Frontends.For(HdlLanguages.Vhdl).LoadDesign(
            new DesignRequest { Root = _dir, Entry = "top", Elaborate = true });

        Assert.Null(design.Error);
        Assert.True(design.Elaborated);
        Assert.Equal(before, Directory.GetFiles(_dir, "*", SearchOption.AllDirectories).Length);
    }

    /// <summary>
    /// Known gap GAP-ELAB-001 (Phase 1): the elaborator's entity regex needs
    /// whitespace after <c>end entity</c>, so the valid VHDL-93 form
    /// <c>end entity;</c> hides the entity and the design is not flattened.
    /// Ratchet: when the engine is fixed this test fails — flip it to assert
    /// <c>Elaborated</c> and drop the gap from the handoff.
    /// </summary>
    [Fact]
    public void KnownGap_end_entity_without_label_is_not_elaborated()
    {
        Write("and2.vhd", AndGate);
        Write("top.vhd", StructuralTop.Replace("end top;", "end entity;"));
        var design = Frontends.For(HdlLanguages.Vhdl).LoadDesign(
            new DesignRequest { Root = _dir, Entry = "top", Elaborate = true });
        Assert.False(design.Elaborated);
    }

    [Fact]
    public void LoadDesign_starts_from_clean_registries()
    {
        Write("and2.vhd", AndGate);
        // Dirty the process-global state the way a previous design would.
        EntityRegistry.Instance.ScanFile(Write("other/leftover.vhd", AndGate.Replace("and2", "leftover")));
        Directory.Delete(Path.Combine(_dir, "other"), recursive: true);
        Assert.NotNull(EntityRegistry.Instance.GetEntity("leftover"));

        var first = Frontends.For(HdlLanguages.Vhdl).LoadDesign(new DesignRequest { Root = _dir });
        Assert.Null(EntityRegistry.Instance.GetEntity("leftover"));

        var second = Frontends.For(HdlLanguages.Vhdl).LoadDesign(new DesignRequest { Root = _dir });
        Assert.Equal(first.Entities.Select(e => e.Name), second.Entities.Select(e => e.Name));
    }

    [Fact]
    public void LoadDesign_keeps_diagnostics_when_the_parse_fails()
    {
        Write("bad.vhd", """
            entity bad is
              port (a : in std_logic b : out std_logic);
            end entity;
            """);
        var design = Frontends.For(HdlLanguages.Vhdl).LoadDesign(new DesignRequest { Root = _dir });

        Assert.NotNull(design.Error);
        Assert.NotNull(design.Unit);
        Assert.ThrowsAny<Exception>(design.ThrowIfFailed);
        // Hosts append build errors to the same list the editor publishes.
        design.Diagnostics.Add(new global::Parser.Models.Diagnostic { Message = "host" });
        Assert.Contains(design.Diagnostics, d => d.Message == "host");
    }

    [Fact]
    public void LoadDesign_reports_missing_sources_without_throwing()
    {
        var design = Frontends.For(HdlLanguages.Vhdl).LoadDesign(new DesignRequest { Root = _dir });
        Assert.IsType<InvalidOperationException>(design.Error);
        Assert.Empty(design.Entities);
    }

    [Fact]
    public void A_registered_language_without_frontend_is_rejected()
    {
        var fake = new HdlLanguage("fakehdl", "FakeHDL", [".fk"], "//", "plaintext", true);
        using (HdlLanguages.RegisterForTesting(fake))
        {
            Assert.Same(fake, HdlLanguages.FromPath("x.fk"));
            Assert.Throws<NotSupportedException>(() => Frontends.For(fake));
        }
        Assert.Null(HdlLanguages.FromPath("x.fk"));
    }
}
