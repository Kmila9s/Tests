using Interpreter.Services;
using Parser.Models;
using static Parser.Models.Constants;

namespace Interpreter.Tests;

/// <summary>
/// Unit tests for the DeltaCycleEngine to verify proper VHDL delta cycle semantics.
/// </summary>
public class DeltaCycleEngineTests
{
    /// <summary>
    /// Concurrent <c>a &lt;= b; b &lt;= a;</c> is a combinational loop: each
    /// delta both signals change and re-trigger each other, so the design never
    /// settles. GHDL stops it at tick 0 ("simulation stopped @0ms by
    /// --stop-delta=5000"); Kmila must likewise report the delta-cycle limit
    /// instead of pretending the pair swapped once. (A one-shot swap only
    /// happens inside a clocked process — covered by the fixture case
    /// Kmila.Shared.Tests/Fixtures/Cases/delta/delta-swap, against GHDL.)
    /// </summary>
    public static async Task<bool> TestDeltaSwapBehavior()
    {
        Console.WriteLine("=== Test: Concurrent swap is a combinational loop ===");

        Variable signalA = new("a", "0", DATATYPES.STD_LOGIC) { PortType = PortType.INOUT };
        Variable signalB = new("b", "1", DATATYPES.STD_LOGIC) { PortType = PortType.INOUT };
        HashSet<Variable> signals = [signalA, signalB];

        List<object> tasks = [new Operation("a <= b", signals), new Operation("b <= a", signals)];

        DeltaCycleEngine engine = new();
        engine.Initialize(signals, tasks);

        try
        {
            await engine.RunTimeStepAsync();
            Console.WriteLine($"FAIL: loop settled at a={signalA.Value}, b={signalB.Value}; GHDL never settles");
            return false;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Delta cycle limit"))
        {
            Console.WriteLine($"PASS: combinational loop detected ({ex.Message})");
            return true;
        }
    }

    /// <summary>
    /// Concurrent rotation <c>a &lt;= b; b &lt;= c; c &lt;= a;</c> with three
    /// distinct values never settles (every delta all three change), like the
    /// two-signal swap. GHDL stops it at tick 0 by --stop-delta; Kmila must
    /// report the delta-cycle limit.
    /// </summary>
    public static async Task<bool> TestDeltaChainAssignment()
    {
        Console.WriteLine("\n=== Test: Concurrent rotation is a combinational loop ===");

        Variable signalA = new("a", "00", DATATYPES.STD_LOGIC_VECTOR) { PortType = PortType.INOUT, Size = 2 };
        Variable signalB = new("b", "01", DATATYPES.STD_LOGIC_VECTOR) { PortType = PortType.INOUT, Size = 2 };
        Variable signalC = new("c", "10", DATATYPES.STD_LOGIC_VECTOR) { PortType = PortType.INOUT, Size = 2 };
        HashSet<Variable> signals = [signalA, signalB, signalC];

        List<object> tasks =
        [
            new Operation("a <= b", signals),
            new Operation("b <= c", signals),
            new Operation("c <= a", signals),
        ];

        DeltaCycleEngine engine = new();
        engine.Initialize(signals, tasks);

        try
        {
            await engine.RunTimeStepAsync();
            Console.WriteLine($"FAIL: loop settled at a={signalA.Value}, b={signalB.Value}, c={signalC.Value}; GHDL never settles");
            return false;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Delta cycle limit"))
        {
            Console.WriteLine($"PASS: combinational loop detected ({ex.Message})");
            return true;
        }
    }

    /// <summary>
    /// Tests that the signal scheduler properly schedules updates.
    /// </summary>
    public static bool TestSignalScheduler()
    {
        Console.WriteLine("\n=== Test: Signal Scheduler ===");

        SignalScheduler scheduler = new();

        Variable signal1 = new("sig1", "0", DATATYPES.STD_LOGIC) { PortType = PortType.INOUT };
        Variable signal2 = new("sig2", "0", DATATYPES.STD_LOGIC) { PortType = PortType.INOUT };

        // Schedule updates
        scheduler.Schedule(signal1, "1");
        scheduler.Schedule(signal2, "1");

        Console.WriteLine($"Before apply: sig1 = {signal1.Value}, sig2 = {signal2.Value}");
        Console.WriteLine($"Pending updates: {scheduler.PendingCount}");

        // Apply updates
        var changed = scheduler.ApplyCurrentUpdates();

        Console.WriteLine($"After apply: sig1 = {signal1.Value}, sig2 = {signal2.Value}");
        Console.WriteLine($"Changed signals: {changed.Count}");

        bool sig1Correct = signal1.Value?.ToString() == "1";
        bool sig2Correct = signal2.Value?.ToString() == "1";
        bool changedCorrect = changed.Count == 2;

        if (sig1Correct && sig2Correct && changedCorrect)
        {
            Console.WriteLine("PASS: Signal scheduler works correctly!");
            return true;
        }
        else
        {
            Console.WriteLine("FAIL: Signal scheduler test failed");
            return false;
        }
    }

    /// <summary>
    /// Tests process scheduler sensitivity tracking.
    /// </summary>
    public static bool TestProcessSchedulerSensitivity()
    {
        Console.WriteLine("\n=== Test: Process Scheduler Sensitivity ===");

        ProcessScheduler scheduler = new();

        // Create signals
        Variable clk = new("clk", "0", DATATYPES.STD_LOGIC) { PortType = PortType.IN };
        Variable rst = new("rst", "0", DATATYPES.STD_LOGIC) { PortType = PortType.IN };
        Variable data = new("data", "00000000", DATATYPES.STD_LOGIC_VECTOR) { PortType = PortType.INOUT, Size = 8 };

        HashSet<Variable> signals = [clk, rst, data];

        // Create a mock process with sensitivity to clk and rst
        // Note: We can't easily create a Process without a TokenStream, so we'll test the scheduler directly
        Console.WriteLine("ProcessScheduler created and ready for sensitivity tracking");
        Console.WriteLine($"Total processes: {scheduler.TotalProcessCount}");

        // Test that scheduler initializes correctly
        bool initCorrect = scheduler.TotalProcessCount == 0;
        bool noPending = !scheduler.HasPendingProcesses;

        if (initCorrect && noPending)
        {
            Console.WriteLine("PASS: Process scheduler initializes correctly!");
            return true;
        }
        else
        {
            Console.WriteLine("FAIL: Process scheduler initialization failed");
            return false;
        }
    }

    /// <summary>
    /// Tests that ScheduledOperation evaluates correctly but schedules instead of assigning.
    /// </summary>
    public static bool TestScheduledOperation()
    {
        Console.WriteLine("\n=== Test: Scheduled Operation ===");

        SignalScheduler scheduler = new();

        Variable result = new("result", "0000", DATATYPES.STD_LOGIC_VECTOR) { PortType = PortType.OUT, Size = 4 };
        Variable input1 = new("input1", "0101", DATATYPES.STD_LOGIC_VECTOR) { PortType = PortType.IN, Size = 4 };

        HashSet<Variable> signals = [result, input1];

        // Create operation: result <= input1
        Operation op = new("result <= input1", signals);

        Console.WriteLine($"Before: result = {result.Value}, input1 = {input1.Value}");

        // Create scheduled operation
        ScheduledOperation scheduledOp = new(op, scheduler);
        scheduledOp.Execute();

        Console.WriteLine($"After execute (before apply): result = {result.Value}");
        Console.WriteLine($"Pending updates: {scheduler.PendingCount}");

        // Result should NOT have changed yet (still scheduled)
        bool notChangedYet = result.Value?.ToString() == "0000";
        bool hasPending = scheduler.PendingCount == 1;

        // Now apply
        scheduler.ApplyCurrentUpdates();

        Console.WriteLine($"After apply: result = {result.Value}");

        bool changedCorrectly = result.Value?.ToString() == "0101";

        if (notChangedYet && hasPending && changedCorrectly)
        {
            Console.WriteLine("PASS: Scheduled operation works correctly!");
            return true;
        }
        else
        {
            Console.WriteLine($"FAIL: notChangedYet={notChangedYet}, hasPending={hasPending}, changedCorrectly={changedCorrectly}");
            return false;
        }
    }

    /// <summary>
    /// Tests signal history recording during delta cycle simulation.
    /// </summary>
    public static async Task<bool> TestSignalHistoryRecording()
    {
        Console.WriteLine("\n=== Test: Signal History Recording ===");

        // Create signals
        Variable signalA = new("a", "0", DATATYPES.STD_LOGIC) { PortType = PortType.INOUT };
        Variable signalB = new("b", "1", DATATYPES.STD_LOGIC) { PortType = PortType.INOUT };

        HashSet<Variable> signals = [signalA, signalB];

        // One settling assignment (a concurrent swap would be a combinational
        // loop that never settles — see TestDeltaSwapBehavior).
        List<object> tasks = [new Operation("a <= b", signals)];

        // Create and initialize the delta cycle engine
        DeltaCycleEngine engine = new();
        engine.Initialize(signals, tasks);

        Console.WriteLine($"Initial values: a = {signalA.Value}, b = {signalB.Value}");
        Console.WriteLine($"Signals registered in history: {engine.History.SignalCount}");

        // Run one time step
        await engine.RunTimeStepAsync();

        Console.WriteLine($"After delta cycle: a = {signalA.Value}, b = {signalB.Value}");
        Console.WriteLine($"Total samples recorded: {engine.History.TotalSamples}");

        // Get history for signal a
        var historyA = engine.History.GetSignalHistory(signalA);
        var historyB = engine.History.GetSignalHistory(signalB);

        Console.WriteLine($"History for 'a': {historyA.Count} entries");
        foreach (var entry in historyA)
        {
            Console.WriteLine($"  t={entry.Item1}: {entry.Item2}");
        }

        Console.WriteLine($"History for 'b': {historyB.Count} entries");
        foreach (var entry in historyB)
        {
            Console.WriteLine($"  t={entry.Item1}: {entry.Item2}");
        }

        // Verify history was recorded
        bool hasInitialA = historyA.Any(h => h.Item1 == 0 && h.Item2?.ToString() == "0");
        bool hasFinalA = historyA.Any(h => h.Item2?.ToString() == "1");
        bool hasInitialB = historyB.Any(h => h.Item1 == 0 && h.Item2?.ToString() == "1");
        bool bUnchanged = historyB.All(h => h.Item2?.ToString() == "1");

        if (hasInitialA && hasFinalA && hasInitialB && bUnchanged)
        {
            Console.WriteLine("PASS: Signal history recorded correctly!");

            // Also test export functionality
            Console.WriteLine("\n--- Export to Text ---");
            Console.WriteLine(engine.History.ExportToText());

            return true;
        }
        else
        {
            Console.WriteLine($"FAIL: History not recorded correctly");
            Console.WriteLine($"  hasInitialA={hasInitialA}, hasFinalA={hasFinalA}");
            Console.WriteLine($"  hasInitialB={hasInitialB}, bUnchanged={bUnchanged}");
            return false;
        }
    }

    /// <summary>
    /// Tests signal history with multiple time steps.
    /// </summary>
    public static async Task<bool> TestSignalHistoryMultipleSteps()
    {
        await Task.CompletedTask;
        Console.WriteLine("\n=== Test: Signal History Multiple Steps ===");

        // Create a counter-like signal
        Variable counter = new("counter", "00", DATATYPES.STD_LOGIC_VECTOR) { PortType = PortType.INOUT, Size = 2 };
        Variable enable = new("enable", "1", DATATYPES.STD_LOGIC) { PortType = PortType.IN };

        HashSet<Variable> signals = [counter, enable];

        // We'll manually record values to simulate multiple time steps
        SignalHistory history = new();
        history.RegisterSignals(signals, initialTime: 0);

        // Simulate multiple time steps
        counter.Value = "00";
        history.RecordChange(counter, 0, counter.Value);

        counter.Value = "01";
        history.RecordChange(counter, 1000, counter.Value);  // t=1

        counter.Value = "10";
        history.RecordChange(counter, 2000, counter.Value);  // t=2

        counter.Value = "11";
        history.RecordChange(counter, 3000, counter.Value);  // t=3

        Console.WriteLine($"Total samples: {history.TotalSamples}");
        Console.WriteLine($"Time range: {history.GetTimeRange()}");

        var counterHistory = history.GetSignalHistory(counter);
        Console.WriteLine($"Counter history: {counterHistory.Count} entries");
        foreach (var entry in counterHistory)
        {
            Console.WriteLine($"  t={entry.Item1}: {entry.Item2}");
        }

        // Test value at specific time
        var valueAt1500 = history.GetValueAtTime(counter, 1500);
        Console.WriteLine($"Value at t=1500: {valueAt1500} (expected: 01)");

        var valueAt2500 = history.GetValueAtTime(counter, 2500);
        Console.WriteLine($"Value at t=2500: {valueAt2500} (expected: 10)");

        // Verify
        bool countCorrect = counterHistory.Count >= 4;
        bool value1500Correct = valueAt1500?.ToString() == "01";
        bool value2500Correct = valueAt2500?.ToString() == "10";

        if (countCorrect && value1500Correct && value2500Correct)
        {
            Console.WriteLine("PASS: Multiple time steps recorded correctly!");

            // Test CSV export
            Console.WriteLine("\n--- CSV Export ---");
            Console.WriteLine(history.ExportToCsv());

            return true;
        }
        else
        {
            Console.WriteLine($"FAIL: countCorrect={countCorrect}, value1500Correct={value1500Correct}, value2500Correct={value2500Correct}");
            return false;
        }
    }

    /// <summary>
    /// Tests VCD export format for GTKWave compatibility.
    /// </summary>
    public static bool TestVcdExport()
    {
        Console.WriteLine("\n=== Test: VCD Export ===");

        // Create signals
        Variable clk = new("clk", "0", DATATYPES.STD_LOGIC) { PortType = PortType.IN };
        Variable counter = new("counter", "0000", DATATYPES.STD_LOGIC_VECTOR) { PortType = PortType.OUT, Size = 4 };
        Variable enable = new("enable", "1", DATATYPES.STD_LOGIC) { PortType = PortType.IN };

        HashSet<Variable> signals = [clk, counter, enable];

        // Create history and register signals
        SignalHistory history = new();
        history.RegisterSignals(signals, initialTime: 0);

        // Simulate a simple clock and counter pattern
        // Time 0: clk=0, counter=0000, enable=1
        history.RecordChange(clk, 0, "0");
        history.RecordChange(counter, 0, "0000");
        history.RecordChange(enable, 0, "1");

        // Time 1000: clk rises
        history.RecordChange(clk, 1000, "1");
        history.RecordChange(counter, 1000, "0001");

        // Time 2000: clk falls
        history.RecordChange(clk, 2000, "0");

        // Time 3000: clk rises
        history.RecordChange(clk, 3000, "1");
        history.RecordChange(counter, 3000, "0010");

        // Time 4000: clk falls
        history.RecordChange(clk, 4000, "0");

        // Time 5000: clk rises
        history.RecordChange(clk, 5000, "1");
        history.RecordChange(counter, 5000, "0011");

        // Export to VCD
        string vcd = history.ExportToVcd("TestModule", Interpreter.Models.PhysicalTime.FsPerNs);

        Console.WriteLine("--- VCD Output ---");
        Console.WriteLine(vcd);
        Console.WriteLine("------------------");

        // Verify VCD format
        bool hasDate = vcd.Contains("$date");
        bool hasVersion = vcd.Contains("$version");
        // Body times are tick*1000+delta, i.e. picoseconds, so the header is
        // always 1ps (SignalHistory.ExportToVcd; bench Phase D fix).
        bool hasTimescale = vcd.Contains("$timescale") && vcd.Contains("1ps");
        bool hasVariables = vcd.Contains("$var") && vcd.Contains("clk") && vcd.Contains("counter");
        bool hasDumpvars = vcd.Contains("$dumpvars");
        bool hasTimeChanges = vcd.Contains("#1000") || vcd.Contains("#2000");

        Console.WriteLine($"Has $date: {hasDate}");
        Console.WriteLine($"Has $version: {hasVersion}");
        Console.WriteLine($"Has $timescale: {hasTimescale}");
        Console.WriteLine($"Has $var definitions: {hasVariables}");
        Console.WriteLine($"Has $dumpvars: {hasDumpvars}");
        Console.WriteLine($"Has time changes: {hasTimeChanges}");

        if (hasDate && hasVersion && hasTimescale && hasVariables && hasDumpvars && hasTimeChanges)
        {
            Console.WriteLine("PASS: VCD export format is correct!");
            return true;
        }
        else
        {
            Console.WriteLine("FAIL: VCD format validation failed");
            return false;
        }
    }

    /// <summary>
    /// Runs all tests and reports results.
    /// </summary>
    public static async Task<int> RunAllTests()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Delta Cycle Engine Tests");
        Console.WriteLine("========================================\n");

        int passed = 0;
        int failed = 0;

        // A test that throws counts as a failure; it must not abort the suite
        // and hide the results of the tests after it.
        static async Task<bool> Run(Func<Task<bool>> test)
        {
            try { return await test(); }
            catch (Exception ex) { Console.WriteLine($"FAIL: unhandled {ex.GetType().Name}: {ex.Message}"); return false; }
        }
        static Task<bool> RunSync(Func<bool> test) => Run(() => Task.FromResult(test()));

        // Test 1: Signal Scheduler
        if (await RunSync(TestSignalScheduler)) passed++; else failed++;

        // Test 2: Scheduled Operation
        if (await RunSync(TestScheduledOperation)) passed++; else failed++;

        // Test 3: Process Scheduler
        if (await RunSync(TestProcessSchedulerSensitivity)) passed++; else failed++;

        // Test 4: Delta Swap
        if (await Run(TestDeltaSwapBehavior)) passed++; else failed++;

        // Test 5: Delta Chain
        if (await Run(TestDeltaChainAssignment)) passed++; else failed++;

        // Test 6: Signal History Recording
        if (await Run(TestSignalHistoryRecording)) passed++; else failed++;

        // Test 7: Signal History Multiple Steps
        if (await Run(TestSignalHistoryMultipleSteps)) passed++; else failed++;

        // Test 8: VCD Export
        if (await RunSync(TestVcdExport)) passed++; else failed++;

        // The GHDL comparisons (counter, delta swap, combinational, shift register)
        // are fixture cases in Kmila.Shared.Tests (Fixtures/Cases/delta), simulated by Kmila.

        Console.WriteLine("\n========================================");
        Console.WriteLine($"Results: {passed} passed, {failed} failed");
        Console.WriteLine("========================================");

        return failed;
    }
}
