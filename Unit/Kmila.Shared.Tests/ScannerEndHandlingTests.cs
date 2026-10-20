using Parser.Models;

namespace Kmila.Shared.Tests;

// Regression coverage for the Debugger line-scanner's architecture/entity end
// handling (2026-10-20 follow-up). Three previously-wrong behaviors:
//   * a mismatched explicit `end architecture <label>;` was silently accepted
//     (the label was ignored) — now a clear error;
//   * a valid bare `end;` was wrongly rejected ("no entity parsed");
//   * a valid end line with a trailing `-- comment` was not recognized (the
//     ';' the scanner keys on was hidden by the comment).
// Drives the real build-path parser (Debugger.Repositories.Parser.ParseFile).
public class ScannerEndHandlingTests
{
    private static Debugger.Repositories.Parser ParseSource(string vhdl)
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"kmila_scan_{Guid.NewGuid():N}.vhd");
        File.WriteAllText(path, vhdl);
        try
        {
            var parser = new Debugger.Repositories.Parser(path);
            parser.ParseFile();
            return parser;
        }
        finally { try { File.Delete(path); } catch { /* best effort */ } }
    }

    private const string Ent =
        "library IEEE;\nuse IEEE.STD_LOGIC_1164.ALL;\n" +
        "entity dut is\n    port (a : in std_logic; y : out std_logic);\nend entity dut;\n";

    [Fact]
    public void MismatchedExplicitEndLabel_IsRejected()
    {
        var vhdl = Ent +
            "architecture Behavioral of dut is\nbegin\n    y <= a;\n" +
            "end architecture Behavoral;\n";   // misspelled label
        var ex = Assert.Throws<ParserException>(() => ParseSource(vhdl));
        Assert.Contains("end label", ex.Message);
    }

    [Fact]
    public void BareEnd_IsAccepted()
    {
        var vhdl =
            "library IEEE;\nuse IEEE.STD_LOGIC_1164.ALL;\n" +
            "entity dut is\n    port (a : in std_logic; y : out std_logic);\nend;\n" +
            "architecture rtl of dut is\nbegin\n    y <= a;\nend;\n";
        var parser = ParseSource(vhdl);
        Assert.Contains(parser.Entities, e => e.Architecture != null);
    }

    [Fact]
    public void TrailingCommentOnEndLine_IsAccepted()
    {
        var vhdl = Ent +
            "architecture Behavioral of dut is\nbegin\n    y <= a;\n" +
            "end architecture Behavioral;  -- closes the architecture\n";
        var parser = ParseSource(vhdl);
        Assert.Contains(parser.Entities, e => e.Architecture != null);
    }

    [Fact]
    public void OneLineEntity_IsAccepted()
    {
        // Covers the item-7 fix: a one-line `entity … ; end entity …;` used to
        // report "no entity parsed". The architecture is kept multi-line here —
        // one-line *architectures* are a separate, still-open scanner gap.
        var vhdl =
            "library IEEE; use IEEE.STD_LOGIC_1164.ALL;\n" +
            "entity ol is port (a : in std_logic; y : out std_logic); end entity ol;\n" +
            "architecture rtl of ol is\nbegin\n    y <= a;\nend architecture rtl;\n";
        var parser = ParseSource(vhdl);
        Assert.Contains(parser.Entities, e => e.Architecture != null);
    }

    [Fact]
    public void OneLineArchitecture_IsAccepted()
    {
        // The whole architecture (declaration, body and closing 'end') on one
        // line used to leave the entity without an architecture.
        var vhdl =
            "library IEEE; use IEEE.STD_LOGIC_1164.ALL;\n" +
            "entity ol is port (a : in std_logic; y : out std_logic); end entity ol;\n" +
            "architecture rtl of ol is begin y <= a; end architecture rtl;\n";
        var parser = ParseSource(vhdl);
        Assert.Contains(parser.Entities, e => e.Architecture != null);
    }
}
