namespace Kmila.Shared.Tests.Content;

/// <summary>
/// Locates the bundled content (<c>App/Kmila/Kmila.Shared/wwwroot</c>) from the
/// test output directory. The Tests repo is checked out next to <c>App/</c>
/// both locally and in CI, so walking up the parents finds it.
/// </summary>
internal static class ContentRoot
{
    private static readonly Lazy<string> _path = new(Locate);

    /// <summary>Absolute path of the <c>wwwroot</c> folder.</summary>
    public static string Path => _path.Value;

    /// <summary>File-open delegate with the same contract the app's catalogs use (paths relative to wwwroot).</summary>
    public static Task<Stream> Open(string relative) =>
        Task.FromResult<Stream>(File.OpenRead(Resolve(relative)));

    /// <summary>True when <paramref name="relative"/> exists under wwwroot.</summary>
    public static bool Exists(string relative) => File.Exists(Resolve(relative));

    /// <summary>Every file under wwwroot matching <paramref name="pattern"/>.</summary>
    public static IEnumerable<string> Find(string pattern) =>
        Directory.EnumerateFiles(Path, pattern, SearchOption.AllDirectories);

    private static string Resolve(string relative) =>
        System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static string Locate() =>
        Kmila.Shared.Services.SharedContentPaths.Find()
        ?? throw new DirectoryNotFoundException("App/Kmila/Kmila.Shared/wwwroot not found above " + AppContext.BaseDirectory);
}
