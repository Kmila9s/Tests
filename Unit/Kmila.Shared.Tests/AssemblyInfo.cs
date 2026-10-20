// The simulation engine exposes process-global singletons
// (SignalAttributeHandler.Instance, EntityRegistry/PackageRegistry/
// FunctionRegistry.Instance, ReportBus.Instance). Any two test classes that
// drive a real simulation therefore share that state, so running them in
// parallel races (e.g. FastRunnerEquivalenceTests vs SignalAttributeTests both
// mutating SignalAttributeHandler.Instance). The suite is small and fast, so we
// simply serialize the whole assembly rather than sprinkle shared [Collection]s.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
