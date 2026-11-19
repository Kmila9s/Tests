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
        Assert.Equal(".vhdl,.vhd", HdlLanguages.AcceptList());
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
    /// GAP-ELAB-001 (closed in v3.0.4): every IEEE 1076-1993 entity closer
    /// form, and a component declaration without the optional <c>is</c>,
    /// must still let the elaborator flatten the hierarchy.
    /// </summary>
    [Theory]
    [InlineData("end top;")]
    [InlineData("end entity;")]
    [InlineData("end entity top;")]
    [InlineData("end;")]
    public void LoadDesign_elaborates_every_entity_closer_form(string closer)
    {
        Write("and2.vhd", AndGate);
        Write("top.vhd", StructuralTop.Replace("end top;", closer));
        var design = Frontends.For(HdlLanguages.Vhdl).LoadDesign(
            new DesignRequest { Root = _dir, Entry = "top", Elaborate = true });

        Assert.Null(design.Error);
        Assert.True(design.Elaborated);
        Assert.Equal("top", design.Top?.Name);
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

    /// <summary>
    /// G21 (v3.1.4): without elaboration (editor, <c>kmila check</c>) an architecture
    /// whose entity lives in another file — and an entity whose architecture does —
    /// parses with the project's other files as companions.
    /// </summary>
    [Fact]
    public void AnalyzeEach_resolves_units_split_across_files()
    {
        var ent = Write("a_and2_ent.vhd", """
            library ieee; use ieee.std_logic_1164.all;
            entity and2 is port (a, b : in std_logic; y : out std_logic); end entity;
            """);
        var arch = Write("b_and2_arch.vhd", """
            library ieee; use ieee.std_logic_1164.all;
            architecture rtl of and2 is begin y <= a and b; end architecture;
            """);
        var analyzed = Frontends.ForPath(ent).AnalyzeEach([ent, arch]);
        Assert.All(analyzed, a => Assert.Null(a.Unit.Error));
        Assert.All(analyzed, a => Assert.NotNull(Assert.Single(a.Unit.Entities, e => e.Name == "and2").Architecture));
    }

    /// <summary>G21: an architecture with no entity anywhere is an error at its line, not a bare exception.</summary>
    [Fact]
    public void An_architecture_without_its_entity_is_a_located_error()
    {
        var arch = Write("orphan.vhd", "library ieee; use ieee.std_logic_1164.all;\narchitecture rtl of ghost is begin end architecture;\n");
        var error = Frontends.ForPath(arch).AnalyzeEach([arch]).Single().Unit.Error;
        var located = Assert.IsAssignableFrom<global::Parser.Models.ParserException>(error);
        Assert.Contains("entity \"ghost\" not found", located.Message);
        Assert.Equal(2, located.Line);
    }

    /// <summary>
    /// G20/G24 without elaboration: a <c>component</c>/<c>configuration</c> instance
    /// and a configuration declaration (nested <c>end for;</c>) parse.
    /// </summary>
    [Fact]
    public void Component_and_configuration_units_parse_without_elaboration()
    {
        var path = Write("cfg.vhd", AndGate + """

            library ieee; use ieee.std_logic_1164.all;
            entity top is port (x, z : in std_logic; q, r : out std_logic); end top;
            architecture structural of top is
              component and2 port (a, b : in std_logic; y : out std_logic); end component;
              for u0 : and2 use entity work.and2(rtl);
            begin
              u0 : component and2 port map (a => x, b => z, y => q);
              u1 : configuration work.cfg_and port map (a => x, b => z, y => r);
            end structural;

            configuration cfg_and of and2 is
              for rtl
              end for;
            end configuration;

            configuration cfg_top of top is
              for structural
                for u0 : and2 use entity work.and2(rtl); end for;
              end for;
            end cfg_top;
            """);
        var unit = Frontends.ForPath(path).AnalyzeEach([path]).Single().Unit;
        Assert.Null(unit.Error);
        Assert.NotNull(Assert.Single(unit.Entities, e => e.Name == "top").Architecture);
    }
}
