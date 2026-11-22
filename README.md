<p align="right"><a href="README.es.md">Español</a></p>

<p align="center"><img src="docs/assets/banner-en.png" alt="Tests — Unit, end-to-end and performance suites" width="100%"></p>

<p align="center"><a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-3c82f6" alt="License: MIT"></a> <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10-475569" alt=".NET 10"></a> <a href="https://kmila.com.mx"><img src="https://img.shields.io/badge/HDL-VHDL-475569" alt="VHDL"></a> <a href="https://github.com/Kmila9s/Tests/releases"><img src="https://img.shields.io/github/v/release/Kmila9s/Tests?color=3c82f6&label=release" alt="Release"></a></p>

<p align="center">
  <a href="https://kmila.com.mx/demo"><b>Try the browser demo</b></a> · <a href="https://kmila.com.mx">Website</a> · <a href="https://github.com/Kmila9s/Documentation/releases">User manual (PDF) and thesis</a>
</p>

## What it is

The test suites of Kmila: xUnit unit tests for the engine and the shared app library, Playwright end-to-end tests, performance benchmarks and the golden-master baselines every change is checked against.

<p align="center"><img src="docs/assets/screenshot.png" alt="" width="90%"></p>

## Quick start

```bash
# with the Kmila repos checked out side by side (see Kmila/build.sh); GHDL 6 on PATH
zsh Tests/run-all.sh
```

## Where it fits

```mermaid
flowchart LR
  kmila["Kmila<br/>app · web · CLI"]
  debugger["Debugger"]
  interpreter["Interpreter"]
  parser["Parser"]
  librarycompiler["LibraryCompiler"]
  timemachine["TimeMachine"]
  practicevalidator["PracticeValidator"]
  simulatorbench["SimulatorBench"]
  tests["Tests"]
  ghdl["GHDL 6<br/>(reference)"]
  kmila-->debugger
  kmila-->interpreter
  kmila-->librarycompiler
  debugger-->interpreter
  interpreter-->parser
  interpreter-->timemachine
  interpreter-->librarycompiler
  librarycompiler-->parser
  practicevalidator-->kmila
  simulatorbench-->kmila
  tests-->kmila
  practicevalidator-.->ghdl
  simulatorbench-.->ghdl
  classDef me fill:#3c82f6,stroke:#1d4ed8,color:#fff
  classDef other fill:#475569,stroke:#334155,color:#fff
  classDef ext fill:#94a3b8,stroke:#64748b,color:#0f172a
  class kmila,debugger,interpreter,parser,librarycompiler,timemachine,practicevalidator,simulatorbench other
  class ghdl ext
  class tests me
  click kmila "https://github.com/Kmila9s/Kmila"
  click debugger "https://github.com/Kmila9s/Debugger"
  click interpreter "https://github.com/Kmila9s/Interpreter"
  click parser "https://github.com/Kmila9s/Parser"
  click librarycompiler "https://github.com/Kmila9s/LibraryCompiler"
  click timemachine "https://github.com/Kmila9s/TimeMachine"
  click practicevalidator "https://github.com/Kmila9s/PracticeValidator"
  click simulatorbench "https://github.com/Kmila9s/SimulatorBench"
  click tests "https://github.com/Kmila9s/Tests"
```

<sub>Arrows point from a project to what it uses (project references). The highlighted box is this repository.</sub>

## Documentation

- [Technical reference (architecture, API, changelog)](docs/reference.md)
- [User manual (PDF) and thesis](https://github.com/Kmila9s/Documentation/releases)

## The Kmila family

| | | |
|---|---|---|
| <img src="https://github.com/Kmila9s/Kmila/raw/main/docs/assets/icon.png" width="28" alt=""> | [**Kmila**](https://github.com/Kmila9s/Kmila) | Write, simulate and debug HDL without an FPGA |
| <img src="https://github.com/Kmila9s/Parser/raw/main/docs/assets/icon.png" width="28" alt=""> | [**Parser**](https://github.com/Kmila9s/Parser) | VHDL lexer and front-door parsing for Kmila |
| <img src="https://github.com/Kmila9s/Interpreter/raw/main/docs/assets/icon.png" width="28" alt=""> | [**Interpreter**](https://github.com/Kmila9s/Interpreter) | Delta-cycle HDL simulation engine and synthesis |
| <img src="https://github.com/Kmila9s/Debugger/raw/main/docs/assets/icon.png" width="28" alt=""> | [**Debugger**](https://github.com/Kmila9s/Debugger) | Structural checks and lint for VHDL designs |
| <img src="https://github.com/Kmila9s/LibraryCompiler/raw/main/docs/assets/icon.png" width="28" alt=""> | [**LibraryCompiler**](https://github.com/Kmila9s/LibraryCompiler) | IEEE libraries, use clauses and primitive lowering |
| <img src="https://github.com/Kmila9s/TimeMachine/raw/main/docs/assets/icon.png" width="28" alt=""> | [**TimeMachine**](https://github.com/Kmila9s/TimeMachine) | Discrete time base: ticks, clocks and events |
| <img src="https://github.com/Kmila9s/PracticeValidator/raw/main/docs/assets/icon.png" width="28" alt=""> | [**PracticeValidator**](https://github.com/Kmila9s/PracticeValidator) | Validates every practice against GHDL |
| <img src="https://github.com/Kmila9s/SimulatorBench/raw/main/docs/assets/icon.png" width="28" alt=""> | [**SimulatorBench**](https://github.com/Kmila9s/SimulatorBench) | Kmila vs GHDL accuracy bench |
| <img src="https://github.com/Kmila9s/Documentation/raw/main/docs/assets/icon.png" width="28" alt=""> | [**Documentation**](https://github.com/Kmila9s/Documentation/releases) | Thesis and user manual (PDF) |

## Contributing

Issues and merge requests are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT © 2026 Ulrich Tamayo Daniel. See [LICENSE](LICENSE).

<sub>Developed as the degree project TT 2026-B059 at IPN ESCOM (Mexico).</sub>
