using System.Text.Json;
using Kmila.Frontend;
using Kmila.Shared.Models;
using Kmila.Shared.Services;
using Kmila.Shared.Services.Practice;

namespace Kmila.Shared.Tests.Content;

/// <summary>
/// The bundled content (Practice, Templates, Learn, Concepts) uses schema v2:
/// every item carries one variant per HDL language it must support, and the
/// app's own loaders read it (v3.0.5). Missing variants are checked for all
/// content at once by <see cref="LanguageCoverageTests"/>.
/// </summary>
public class ContentSchemaTests
{
    [Fact]
    public async Task Practice_every_exercise_loads_with_non_empty_solutions()
    {
        var catalog = new PracticeCatalog(ContentRoot.Open);
        var meta = await catalog.ListAsync();
        Assert.Equal(135, meta.Count);

        foreach (var m in meta)
        {
            var exercise = await catalog.GetAsync(m.Id);
            Assert.True(exercise != null, $"{m.Id} did not load");
            foreach (var language in exercise!.Variants.Languages)
                Assert.False(string.IsNullOrWhiteSpace(exercise.Variant(language).Solution), $"{m.Id}/{language}: empty solution");
        }
    }

    [Fact]
    public async Task Practice_catalog_rejects_a_v1_manifest()
    {
        const string v1 = """{ "version": 1, "exercises": [ { "id": "x", "modality": "FromZero", "difficulty": "Easy", "folder": "x" } ] }""";
        var catalog = new PracticeCatalog(path => path == "Practice/manifest.json"
            ? Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(v1)))
            : ContentRoot.Open(path));
        Assert.Empty(await catalog.ListAsync());
    }

    [Fact]
    public async Task Templates_every_variant_lists_files_that_exist()
    {
        var templates = await ContentRoot.Templates.ListAsync();
        Assert.Equal(5, templates.Count);

        foreach (var t in templates)
        {
            foreach (var language in t.Variants.Languages)
            {
                var variant = t.Variants.For(language)!;
                Assert.NotEmpty(variant.Files);
                foreach (var f in variant.Files)
                    Assert.True(ContentRoot.Exists($"Templates/{t.Id}/en/files/{f.Path}"), $"{t.Id}: missing {f.Path}");
                var paths = variant.Files.Select(f => f.Path).ToHashSet();
                Assert.All(variant.LibraryFiles, lf => Assert.Contains(lf, paths));
            }
        }
    }

    [Fact]
    public async Task Learn_samples_are_in_their_language_and_not_empty()
    {
        var manifest = await ContentRoot.Lessons.GetManifestAsync();
        Assert.Equal(ContentSchema.Version, manifest.Version);
        Assert.Equal(14, manifest.Modules.Count);

        var samples = 0;
        foreach (var module in manifest.Modules)
        {
            foreach (var lesson in module.Lessons.Where(l => l.Sample != null))
            {
                samples++;
                var variants = lesson.Sample!.ByLanguage;
                foreach (var language in variants.Languages)
                {
                    var source = variants.For(language)!;
                    Assert.True(language.Owns(source.FileName), $"{module.Id}/{lesson.Id}: {source.FileName} is not {language.DisplayName}");
                    Assert.False(string.IsNullOrWhiteSpace(source.Content), $"{module.Id}/{lesson.Id}: empty sample");
                }
            }
        }
        Assert.Equal(15, samples);
    }

    [Fact]
    public void Timelines_declare_a_registered_language_or_plaintext()
    {
        var allowed = HdlLanguages.Supported.Select(l => l.MonacoId).Append("plaintext").ToHashSet();
        var timelines = ContentRoot.Find("*.timeline.json").ToList();
        Assert.NotEmpty(timelines);

        foreach (var path in timelines)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("language", out var lang))
                Assert.True(allowed.Contains(lang.GetString() ?? ""), $"{path}: language '{lang}'");
        }
    }
}
