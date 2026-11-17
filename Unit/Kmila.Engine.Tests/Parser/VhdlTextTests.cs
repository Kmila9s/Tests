using Parser.Models;

namespace Kmila.Engine.Tests.Parser;

/// <summary>FE-8: the single string-aware comment/literal helper every VHDL text scanner uses.</summary>
public class VhdlTextTests
{
    private const string Source = "report \"-- not a comment\"; -- comment\nq <= a; -- x\n\"--\"";

    [Fact]
    public void Line_comments_inside_strings_are_kept() =>
        Assert.Equal("report \"-- not a comment\"; \nq <= a; \n\"--\"", VhdlText.StripLineComments(Source));

    [Fact]
    public void Blanking_keeps_every_offset() =>
        Assert.Equal("report \"-- not a comment\";           \nq <= a;     \n\"--\"", VhdlText.BlankLineComments(Source));

    [Fact]
    public void Masking_replaces_string_contents_and_keeps_length()
    {
        var masked = VhdlText.MaskStringLiterals("report \"end of test\";\nend;");
        Assert.Equal("report \"...........\";\nend;", masked);
    }

    [Fact]
    public void An_unterminated_string_ends_at_the_line_break() =>
        Assert.Equal("x <= \"ab\nq <= b; ", VhdlText.StripLineComments("x <= \"ab\nq <= b; -- c"));
}
