// The engine keeps process-global state (EntityRegistry, PackageRegistry,
// FunctionRegistry, SignalAttributeHandler, IEEELibraryLoader, …): the Interpreter
// suites and the front-end tests mutate it, so the assembly runs serially, as
// Kmila.Shared.Tests does.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
