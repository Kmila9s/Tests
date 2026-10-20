using Interpreter.Repositories;
using Parser.Models;
using static Parser.Models.Constants;

namespace Kmila.Shared.Tests;

// STATUS §1 (2026-07-05) — with…select alternative-list gap. `when 0|1|7`
// against a bit-string selector value ("0001") used to fall through to
// the `others` branch because CleanValue string-equaled "0001" against
// "1". These tests pin the numeric-normalisation path.
public class SelectedAssignmentTests
{
    private static Variable Sig(string name, string value, int size = 8)
    {
        // std_logic_vector signal with a binary string value.
        var v = new Variable(name, value, DATATYPES.STD_LOGIC_VECTOR, size);
        return v;
    }

    private static SelectedAssignment BuildAssignment(string selector, string target,
        HashSet<Variable> vars, params (string value, string[] whens)[] branches)
    {
        var a = new SelectedAssignment(vars, selector, target);
        foreach (var (value, whens) in branches)
        {
            var c = new SelectionChoice { ValueExpression = value };
            foreach (var w in whens)
            {
                if (w == "others") c.IsOthers = true;
                else c.Choices.Add(w);
            }
            a.Choices.Add(c);
        }
        return a;
    }

    [Fact]
    public async Task WithSelect_IntegerAlternativeVsBitStringSelector_Matches()
    {
        // sel = "0001" (bit-string 4-wide) — used to miss `when 1`.
        var sel = Sig("sel", "0001", 4);
        var y = Sig("y", "0000", 4);
        var vars = new HashSet<Variable> { sel, y };
        var a = BuildAssignment("sel", "y", vars,
            ("\"1111\"", new[] { "0" }),
            ("\"0001\"", new[] { "1" }),
            ("\"0000\"", new[] { "others" }));

        await a.ExecuteAsync();

        // "1" branch should have matched sel="0001" (both = 1).
        Assert.Equal("0001", y.Value);
    }

    [Fact]
    public async Task WithSelect_AlternativeList_MatchesAnyMember()
    {
        // sel = "0111" (7) — must match the {0,1,7} alternative list.
        var sel = Sig("sel", "0111", 4);
        var y = Sig("y", "0000", 4);
        var vars = new HashSet<Variable> { sel, y };
        var a = BuildAssignment("sel", "y", vars,
            ("\"1010\"", new[] { "0", "1", "7" }),
            ("\"0000\"", new[] { "others" }));

        await a.ExecuteAsync();

        Assert.Equal("1010", y.Value);
    }

    [Fact]
    public async Task WithSelect_NoMatch_FallsThroughToOthers()
    {
        var sel = Sig("sel", "1111", 4);   // = 15
        var y = Sig("y", "0000", 4);
        var vars = new HashSet<Variable> { sel, y };
        var a = BuildAssignment("sel", "y", vars,
            ("\"1010\"", new[] { "0", "1", "7" }),
            ("\"0011\"", new[] { "others" }));

        await a.ExecuteAsync();

        Assert.Equal("0011", y.Value);
    }

    [Fact]
    public async Task WithSelect_BitStringVsBitString_StillMatches()
    {
        // Original path — both sides are bit-strings — must still work.
        var sel = Sig("sel", "01", 2);
        var y = Sig("y", "0", 1);
        var vars = new HashSet<Variable> { sel, y };
        var a = BuildAssignment("sel", "y", vars,
            ("'1'", new[] { "\"01\"" }),
            ("'0'", new[] { "others" }));

        await a.ExecuteAsync();

        Assert.Equal("1", y.Value);
    }
}
