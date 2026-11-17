using Kmila.Frontend;

namespace Kmila.Engine.Tests;

/// <summary>
/// Single place where engine tests run the production front-end: clean
/// registries, scan the file's declarations, parse it.
/// </summary>
public static class EngineFixture
{
    public static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    /// <summary>
    /// Analyses one HDL file the way <c>kmila check</c> does and returns the
    /// parsed unit. Throws the original parse exception when it fails.
    /// </summary>
    public static ParsedUnit Parse(string path)
    {
        var unit = Analyze(path);
        unit.ThrowIfFailed();
        return unit;
    }

    /// <summary>Analyses one HDL file like <c>kmila check</c>; a failure stays in <see cref="ParsedUnit.Error"/>.</summary>
    public static ParsedUnit Analyze(string path) =>
        Frontends.ForPath(path).AnalyzeEach([path]).Single().Unit;

    /// <summary><see cref="Analyze(string)"/> over source text (written to a temporary <c>.vhdl</c> file).</summary>
    public static ParsedUnit AnalyzeSource(string vhdl)
    {
        var dir = Directory.CreateTempSubdirectory("kmila-engine-").FullName;
        try
        {
            var path = Path.Combine(dir, "design.vhdl");
            File.WriteAllText(path, vhdl);
            return Analyze(path);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
