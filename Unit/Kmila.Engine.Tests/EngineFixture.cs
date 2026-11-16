using Interpreter.Services;

namespace Kmila.Engine.Tests;

/// <summary>
/// Single place where engine tests prepare the process-wide registries and
/// run the production parser (<see cref="Debugger.Repositories.Parser"/>).
/// When the language-neutral front-end lands (Phase 0, Kmila.Frontend) this
/// helper delegates to it instead of repeating the registry sequence.
/// </summary>
public static class EngineFixture
{
    /// <summary>Serialises tests that touch the global registries.</summary>
    public static readonly object RegistryLock = new();

    public static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    /// <summary>
    /// Parses one VHDL file the way <c>kmila check</c> does: clean registries,
    /// scan the file's entities/packages/declared names, then run the parser.
    /// Returns the parser so callers can inspect entities and diagnostics.
    /// </summary>
    public static Debugger.Repositories.Parser Parse(string path)
    {
        lock (RegistryLock)
        {
            EntityRegistry.Instance.Clear();
            PackageRegistry.Instance.Clear();
            FunctionRegistry.Instance.Clear();
            EntityRegistry.Instance.ScanFile(path);
            PackageSubtypeScanner.ScanFile(path);
            PackageRegistry.Instance.RegisterDeclaredNamesFromSource(File.ReadAllText(path));

            var parser = new Debugger.Repositories.Parser(path);
            parser.ParseFile();
            return parser;
        }
    }
}
