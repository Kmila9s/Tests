using Parser.Models;
using Parser.Services;
using Xunit;

namespace Parser.Tests;

/// <summary>
/// Tests for <see cref="UserTypeRegistry"/> and its integration with
/// <see cref="Constants.FindTypoSuggestion"/>. The host (Kmila) seeds the
/// registry from compiled-library <c>Type</c> symbols before the parser
/// runs; here we exercise the suggestion path directly.
/// </summary>
public class UserTypeRegistryTests
{
    // The registry is a process-wide singleton — every test clears it first
    // so leakage from a previous test can't change suggestion outcomes.
    /// <summary>Clears the shared registry so each test starts clean.</summary>
    public UserTypeRegistryTests() => UserTypeRegistry.Instance.Clear();

    /// <summary>A misspelled user-defined type name yields the registered type as a suggestion.</summary>
    [Fact]
    public void FindTypoSuggestion_FallsBack_To_UserDefinedType()
    {
        UserTypeRegistry.Instance.Register("coefficient_array_t");

        var suggestion = Constants.FindTypoSuggestion("coeficient_array_t");

        Assert.Equal("coefficient_array_t", suggestion);
    }

    /// <summary>A canonical built-in wins over a user type when it is the closer match.</summary>
    [Fact]
    public void FindTypoSuggestion_PrefersCloserMatch_Across_BuiltinsAndUserTypes()
    {
        // "std_logoc" is distance 1 from "std_logic" (canonical) and farther
        // from any user-defined type — canonical wins.
        UserTypeRegistry.Instance.Register("status_register_t");

        var suggestion = Constants.FindTypoSuggestion("std_logoc");

        Assert.Equal("std_logic", suggestion);
    }

    /// <summary>An exact (case-insensitive) user-type match is not a typo and returns no suggestion.</summary>
    [Fact]
    public void FindTypoSuggestion_Returns_Null_For_Exact_UserType_Match()
    {
        UserTypeRegistry.Instance.Register("coefficient_array_t");

        // Caller typed the exact name — that's not a typo, no suggestion.
        Assert.Null(Constants.FindTypoSuggestion("coefficient_array_t"));
        // Case-insensitive, same outcome.
        Assert.Null(Constants.FindTypoSuggestion("COEFFICIENT_ARRAY_T"));
    }

    /// <summary>A token unlike any built-in or user type returns no suggestion.</summary>
    [Fact]
    public void FindTypoSuggestion_Returns_Null_When_Too_Far_From_Both_Sets()
    {
        UserTypeRegistry.Instance.Register("coefficient_array_t");

        // A token totally unlike any candidate type → not a typo, no suggestion.
        Assert.Null(Constants.FindTypoSuggestion("widget"));
    }

    /// <summary>Clearing the registry removes every previously registered type.</summary>
    [Fact]
    public void Clear_Drops_All_Registered_Types()
    {
        UserTypeRegistry.Instance.Register("alpha_t");
        UserTypeRegistry.Instance.Register("beta_t");
        Assert.True(UserTypeRegistry.Instance.IsKnown("alpha_t"));

        UserTypeRegistry.Instance.Clear();

        Assert.False(UserTypeRegistry.Instance.IsKnown("alpha_t"));
        Assert.False(UserTypeRegistry.Instance.IsKnown("beta_t"));
        Assert.Empty(UserTypeRegistry.Instance.All);
    }

    /// <summary>Registration is case-insensitive and ignores empty or whitespace names.</summary>
    [Fact]
    public void Register_Is_CaseInsensitive_And_NoOp_For_Empty()
    {
        UserTypeRegistry.Instance.Register("MyType_T");
        UserTypeRegistry.Instance.Register("");
        UserTypeRegistry.Instance.Register("   ");

        Assert.True(UserTypeRegistry.Instance.IsKnown("mytype_t"));
        Assert.Single(UserTypeRegistry.Instance.All);
    }
}
