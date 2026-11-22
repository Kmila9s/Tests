using Kmila.Frontend;
using Kmila.TestKit.Cases;

namespace Kmila.Shared.Tests.Support;

/// <summary>
/// Theory data over the multi-variant fixture cases (<c>Fixtures/Cases/&lt;group&gt;/&lt;case&gt;</c>):
/// one row per (case × supported HDL language), so a case without a variant for a
/// supported language produces a row that fails instead of disappearing.
/// </summary>
public static class HdlVariantCases
{
    /// <summary>The fixture cases root in the test output.</summary>
    public static string Root => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cases");

    /// <summary>(case folder relative to <see cref="Root"/>, language id) for every case and supported language.</summary>
    public static TheoryData<string, string> All()
    {
        var data = new TheoryData<string, string>();
        foreach (var c in CaseLoader.FindAll(Root))
            foreach (var language in HdlLanguages.Supported)
                data.Add(Path.GetRelativePath(Root, c.Dir), language.Id);
        return data;
    }
}
