using Kmila.Frontend;
using Kmila.Shared.Services.Practice;
using Kmila.Shared.Tests.Content;
using Kmila.TestKit.Cases;

namespace Kmila.Shared.Tests.Support;

/// <summary>Kinds of multi-variant content.</summary>
public enum ContentKind { Practice, Template, Learn, Fixture }

/// <summary>
/// One content item that must exist in several HDL languages, and how to ask it
/// which required languages it lacks (evaluated on demand, so a language registered
/// afterwards is required too).
/// </summary>
public sealed record CoverageItem(ContentKind Kind, string Id, Func<IEnumerable<HdlLanguage>> Missing)
{
    /// <summary>"Kind id", for messages.</summary>
    public string Label => $"{Kind} {Id}";
}

/// <summary>
/// Every multi-variant content item the repository's tests can reach: practice
/// exercises, project templates, Learn samples (their module decides which
/// languages it requires) and the unit-test fixture cases. The bench enforces its
/// own cases with <c>VariantVerdict</c>.
/// </summary>
public static class ContentCoverage
{
    /// <summary>All items, practice first.</summary>
    public static async Task<List<CoverageItem>> AllAsync()
    {
        var items = new List<CoverageItem>();

        var catalog = new PracticeCatalog(ContentRoot.Open);
        foreach (var m in await catalog.ListAsync())
        {
            var exercise = await catalog.GetAsync(m.Id) ?? throw new InvalidOperationException($"{m.Id} did not load");
            items.Add(new(ContentKind.Practice, m.Id, () => exercise.Variants.MissingFrom(HdlLanguages.Supported)));
        }

        foreach (var t in await ContentRoot.Templates.ListAsync())
            items.Add(new(ContentKind.Template, t.Id, () => t.Variants.MissingFrom(HdlLanguages.Supported)));

        var manifest = await ContentRoot.Lessons.GetManifestAsync();
        foreach (var module in manifest.Modules)
            foreach (var lesson in module.Lessons.Where(l => l.Sample != null))
                items.Add(new(ContentKind.Learn, $"{module.Id}/{lesson.Id}", () => lesson.Sample!.ByLanguage.MissingFrom(module.RequiredLanguages())));

        foreach (var c in CaseLoader.FindAll(HdlVariantCases.Root))
            items.Add(new(ContentKind.Fixture, c.Spec.Id, () => c.Variants.MissingFrom(HdlLanguages.Supported)));
        return items;
    }
}
