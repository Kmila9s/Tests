using Parser.Repositories;
using Parser.Models;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace Parser.Tests;

/// <summary>
/// Unit tests for the <see cref="Tokenizer"/> class, verifying correct tokenization
/// of various VHDL input patterns including declarations, assignments, comments, and edge cases.
/// </summary>
public class TokenizerTests
{
    /// <summary>
    /// Verifies that the tokenizer produces the correct number of tokens, first token, and last token
    /// for a variety of VHDL input lines.
    /// </summary>
    /// <param name="vhdlLine">The VHDL source line to tokenize.</param>
    /// <param name="expectedTokenCount">Expected number of tokens produced.</param>
    /// <param name="expectedFirstToken">Expected value of the first token, or null if empty.</param>
    /// <param name="expectedLastToken">Expected value of the last token, or null if empty.</param>
    [Theory]
    [InlineData("entity MyCounter is", 3, "entity", "is")] // Simple entity declaration
    [InlineData("signal clk : std_logic; -- This is a clock signal", 5, "signal", ";")] // Declaration with a comment
    [InlineData("a <= b;", 4, "a", ";")] // Simple assignment
    [InlineData("data : out std_logic_vector(7 downto 0);", 10, "data", ";")] // Port declaration with vector
    [InlineData("-- This is a full line comment", 0, null, null)] // A line that is only a comment
    [InlineData("", 0, null, null)] // An empty line
    [InlineData("    ", 0, null, null)] // A line with only whitespace
    public void TokenizeLine_VariousInputs_ShouldProduceCorrectTokens(string vhdlLine, int expectedTokenCount, string? expectedFirstToken, string? expectedLastToken)
    {
        // Arrange
        var tokenizer = new Tokenizer();

        // Act
        tokenizer.ReadString(vhdlLine);
        tokenizer.Evaluate();
        List<Token> tokens = tokenizer.GetDictionaryType();

        // Assert
        Assert.Equal(expectedTokenCount, tokens.Count);

        if (expectedTokenCount > 0)
        {
            Assert.Equal(expectedFirstToken, tokens.First().Value);
            Assert.Equal(expectedLastToken, tokens.Last().Value);
        }
    }
}