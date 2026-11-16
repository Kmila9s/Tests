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
        var (_, unit) = Frontends.ForPath(path).AnalyzeEach([path]).Single();
        unit.ThrowIfFailed();
        return unit;
    }
}
