using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// BlockProgram.Fingerprint is what binds a student's Blockly workspace to
// the pre-baked ExercisePayload catalog. If sort stability, coord-drop, or
// key normalisation drift, every concept exercise stops matching silently.
// These tests are the invariant.
public class BlockProgramTests
{
    private static BlockProgram NewProgram() => new(logger: null);

    [Fact]
    public void Fingerprint_NullOrEmpty_ReturnsEmpty()
    {
        var p = NewProgram();
        Assert.Equal(string.Empty, p.Fingerprint(null));
        Assert.Equal(string.Empty, p.Fingerprint(""));
        Assert.Equal(string.Empty, p.Fingerprint("   "));
    }

    [Fact]
    public void Fingerprint_MalformedJson_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NewProgram().Fingerprint("{not-json"));
    }

    [Fact]
    public void Fingerprint_EmptyWorkspace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NewProgram().Fingerprint("""{"blocks":{"blocks":[]}}"""));
    }

    // Coordinates must not participate. Two students who dragged the same
    // program into different canvas positions should share a fingerprint.
    [Fact]
    public void Fingerprint_SameProgramDifferentCoords_ProducesSameFingerprint()
    {
        const string a = """
            {"blocks":{"blocks":[
              {"type":"kmila_vhdl_entity","id":"e1","x":10,"y":20,"fields":{"NAME":"foo"}}
            ]}}
            """;
        const string b = """
            {"blocks":{"blocks":[
              {"type":"kmila_vhdl_entity","id":"e2","x":500,"y":800,"fields":{"NAME":"foo"}}
            ]}}
            """;
        var p = NewProgram();
        Assert.Equal(p.Fingerprint(a), p.Fingerprint(b));
    }

    // Field key order must be normalized. Blockly's JSON serializer is
    // not order-stable across versions — the fingerprint has to be.
    [Fact]
    public void Fingerprint_FieldKeyOrderShuffled_ProducesSameFingerprint()
    {
        const string a = """
            {"blocks":{"blocks":[
              {"type":"kmila_vhdl_port","fields":{"NAME":"clk","DIR":"in","TYPE":"std_logic"}}
            ]}}
            """;
        const string b = """
            {"blocks":{"blocks":[
              {"type":"kmila_vhdl_port","fields":{"TYPE":"std_logic","DIR":"in","NAME":"clk"}}
            ]}}
            """;
        var p = NewProgram();
        Assert.Equal(p.Fingerprint(a), p.Fingerprint(b));
    }

    // Real programs (Build output) fingerprint stably across two runs.
    [Fact]
    public void Fingerprint_SameSourceTwice_IsDeterministic()
    {
        const string src = """
            entity foo is port (a : in std_logic; y : out std_logic); end foo;
            architecture rtl of foo is begin y <= a; end rtl;
            """;
        var w1 = new VhdlBlockBuilder().Build(src);
        var w2 = new VhdlBlockBuilder().Build(src);
        var p = NewProgram();
        Assert.Equal(p.Fingerprint(w1), p.Fingerprint(w2));
        Assert.NotEqual(string.Empty, p.Fingerprint(w1));
    }

    // Different programs must fingerprint differently — otherwise every
    // student would match the same catalog entry.
    [Fact]
    public void Fingerprint_DifferentPrograms_DifferentFingerprints()
    {
        const string src1 = """
            entity foo is port (a : in std_logic; y : out std_logic); end foo;
            architecture rtl of foo is begin y <= a; end rtl;
            """;
        const string src2 = """
            entity foo is port (a : in std_logic; y : out std_logic); end foo;
            architecture rtl of foo is begin y <= not a; end rtl;
            """;
        var p = NewProgram();
        var f1 = p.Fingerprint(new VhdlBlockBuilder().Build(src1));
        var f2 = p.Fingerprint(new VhdlBlockBuilder().Build(src2));
        Assert.NotEqual(f1, f2);
    }
}
