## xUnit

### Purpose

xUnit v3 (`xunit.v3` and `xunit.runner.visualstudio`) is used as the unit-testing framework for
every DemaConsulting.DocDown test project. It was chosen as the standard, widely supported .NET test
framework
that discovers and executes `[Fact]`/`[Theory]` test methods and produces TRX results consumable
by downstream tooling.

### Features Used

- Test discovery and execution of `[Fact]` and `[Theory]` test methods in every test project:
  `DemaConsulting.DocDown.Core.Tests`, `Pdf.Tests`, `Pdf.Rendering.Tests`, `Office.Tests`,
  `PowerPoint.Rendering.Tests`, `Visio.Rendering.Tests`, and `Tool.Tests`
- TRX result-file generation, consumed by ReqStream for requirements-to-test traceability

### Integration Pattern

xUnit is invoked indirectly through `dotnet test` in `build.ps1` and in the test jobs of
`.github/workflows/build.yaml`; there is no direct CLI invocation of an `xunit` tool by name.
It is a stateless test-run invocation — each run discovers tests in the compiled test assembly,
executes them, writes TRX output, and exits; there is no initialization, configuration object, or
disposal step beyond the standard .NET test-project configuration (`.csproj` test SDK settings).
