using Parser.Models;

namespace Kmila.Engine.Tests.Parser;

/// <summary>
/// The VHDL corpus formerly swept by <c>Parser/Program.cs</c> against the
/// unused V2 parser. It now runs through the production parser, so a file
/// here passing means the app accepts it unmodified.
/// </summary>
public class ParserCorpusTests
{
    /// <summary>
    /// Known gaps: valid VHDL the production parser still rejects. Tracked so
    /// the list can only shrink; the source files are never edited to pass.
    /// </summary>
    public static readonly Dictionary<string, string> KnownGaps = new()
    {
        // GAP-PAR-001 (test40.vhdl, underscores in x"3243_1DAC") closed in v3.0.4.
    };

    public static TheoryData<string> CorpusFiles()
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.GetFiles(EngineFixture.FixturePath("ParserCorpus"), "test*.vhdl")
                                   .Where(f => !Path.GetFileName(f).StartsWith("test_negative_"))
                                   .Order())
            data.Add(Path.GetFileName(f));
        return data;
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void Corpus_file_parses_with_production_parser(string file)
    {
        var path = EngineFixture.FixturePath("ParserCorpus", file);
        if (KnownGaps.TryGetValue(file, out var gap))
        {
            // Ratchet: a known gap must still fail. When it starts passing,
            // remove it from KnownGaps.
            Assert.ThrowsAny<Exception>(() => EngineFixture.Parse(path));
            return;
        }

        var parser = EngineFixture.Parse(path);
        Assert.NotEmpty(parser.Entities);
    }

    [Theory]
    [InlineData("test_negative_unknown_type.vhdl", "STDLOGIC")]
    [InlineData("test_negative_missing_port_semi.vhdl", "Missing ';'")]
    [InlineData("test_negative_garbage_in_body.vhdl", "Unexpected token '$'")]
    public void Invalid_vhdl_is_rejected_with_a_specific_message(string file, string mustContain)
    {
        var path = EngineFixture.FixturePath("ParserCorpus", file);
        var ex = Assert.ThrowsAny<Exception>(() => EngineFixture.Parse(path));
        Assert.Contains(mustContain, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
