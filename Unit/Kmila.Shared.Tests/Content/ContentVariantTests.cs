using Kmila.Frontend;
using Kmila.Shared.Services.Api;
using Kmila.TestKit.Runners;

namespace Kmila.Shared.Tests.Content;

/// <summary>
/// v3.0.11 (Fase 0, 5e): the content a student opens works in every variant it
/// ships — each project template and each Learn sample loads through its
/// language's front-end and simulates 1 µs in Kmila without an error (a library
/// template, which has no top unit, only has to load).
/// </summary>
public class ContentVariantTests
{
    private const int OneMicrosecondNs = 1_000;

    public static TheoryData<string, string> Templates()
    {
        var data = new TheoryData<string, string>();
        foreach (var t in ContentRoot.Templates.ListAsync().GetAwaiter().GetResult())
            foreach (var language in t.Variants.Languages)
                data.Add(t.Id, language.Id);
        return data;
    }

    public static TheoryData<string, string, string> Samples()
    {
        var data = new TheoryData<string, string, string>();
        var manifest = ContentRoot.Lessons.GetManifestAsync().GetAwaiter().GetResult();
        foreach (var module in manifest.Modules)
            foreach (var lesson in module.Lessons.Where(l => l.Sample != null))
                foreach (var language in lesson.Sample!.ByLanguage.Languages)
                    data.Add(module.Id, lesson.Id, language.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task Template_simulates_in_each_variant(string templateId, string languageId)
    {
        var language = HdlLanguages.FromId(languageId)!;
        var service = ContentRoot.Templates;
        var template = (await service.ListAsync()).Single(t => t.Id == templateId);
        var variant = template.Variants.For(language)!;
        // The files a new project gets (tokens substituted, as the app writes them); the HDL ones are the design.
        var files = (await service.RenderFilesAsync(template, variant, "Kmila Content Test"))
            .Where(f => language.Owns(f.Path))
            .Select(f => new SourceFile(Path.GetFileName(f.Path), f.Content))
            .ToList();
        Assert.NotEmpty(files);

        if (variant.IsLibrary)
        {
            TestDesigns.Load(language, files.Select(f => (f.Name, f.Content))).ThrowIfFailed();
            return;
        }
        await AssertSimulates(files, $"template {templateId} [{languageId}]");
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Learn_sample_simulates_in_each_variant(string moduleId, string lessonId, string languageId)
    {
        var manifest = await ContentRoot.Lessons.GetManifestAsync();
        var lesson = manifest.Modules.Single(m => m.Id == moduleId).Lessons.Single(l => l.Id == lessonId);
        var source = lesson.Sample!.ByLanguage.For(HdlLanguages.FromId(languageId)!)!;
        await AssertSimulates([new SourceFile(source.FileName, source.Content)], $"learn {moduleId}/{lessonId} [{languageId}]");
    }

    private static async Task AssertSimulates(IReadOnlyList<SourceFile> files, string label)
    {
        var response = await new InProcessKmilaRunner().SimulateAsync(files, new SimulateRequest { Duration = OneMicrosecondNs });
        Assert.True(response.Error is null, $"{label}: {response.Error}");
        Assert.False(string.IsNullOrWhiteSpace(response.Vcd), $"{label}: no waveform");
    }
}
