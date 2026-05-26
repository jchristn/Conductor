# Conductor Testing Guide

Conductor’s automated test suite now uses **Touchstone** so the same shared test cases can run through multiple hosts without duplicating test logic.

## Projects

| Project | Location | Purpose |
|---|---|---|
| `Test.Shared` | `src/Test.Shared/` | Authoritative source for all test cases and Touchstone descriptors |
| `Test.Automated` | `src/Test.Automated/` | Console runner built on `Touchstone.Cli` |
| `Test.Xunit` | `src/Test.Xunit/` | xUnit host built on `Touchstone.XunitAdapter` |
| `Test.Nunit` | `src/Test.Nunit/` | NUnit host built on `Touchstone.NunitAdapter` |

`Test.Shared` contains the real tests. `Test.Xunit` and `Test.Nunit` each expose the same descriptors through their own framework adapters, so `dotnet test src/Conductor.sln` runs the shared suite twice on purpose.

## Quick Start

Run the framework hosts through the solution:

```bash
dotnet test src/Conductor.sln
```

Run only the xUnit host:

```bash
dotnet test src/Test.Xunit/Test.Xunit.csproj
```

Run only the NUnit host:

```bash
dotnet test src/Test.Nunit/Test.Nunit.csproj
```

Run the console host:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj
```

`Test.Shared` is not a standalone test host. Running `dotnet test src/Test.Shared/Test.Shared.csproj` only compiles the shared descriptors; it does not execute them. Use `Test.Automated`, `Test.Xunit`, or `Test.Nunit` when you need real execution.

Write console results to JSON:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --results TestResults/touchstone-results.json
```

Collect coverage from a framework host:

```bash
dotnet test src/Test.Xunit/Test.Xunit.csproj --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## How It Works

`Test.Shared` uses a reflection-based Touchstone registry:

- Any non-abstract public class in `src/Test.Shared/` whose name ends with `Tests` becomes a Touchstone suite.
- Any public parameterless `void`, `Task`, or `ValueTask` method on that class becomes a Touchstone test case.
- `Initialize`, `InitializeAsync`, `Dispose`, and `DisposeAsync` are treated as per-test lifecycle hooks and are not exposed as test cases.
- Descriptor display names are emitted as `ClassName.MethodName`.

This preserves the original xUnit-style “new class instance per test” behavior while making the test definitions runner-agnostic.

## Writing Tests

Add new tests under `src/Test.Shared/` and keep the existing structure by domain:

- `Core/Enums`
- `Core/Helpers`
- `Core/Models`
- `Core/Settings`
- `Server/Controllers`
- `Server/Integration`
- `Server/Services`

Guidelines:

- Name files `*Tests.cs`.
- Add tests as public parameterless methods returning `void`, `Task`, or `ValueTask`.
- Use optional `InitializeAsync`/`Dispose` hooks when a test class needs setup or cleanup.
- Keep using `FluentAssertions`.
- Prefer explicit positive and negative coverage for any behavior you touch.

## Feature Expectations

Changes in the following areas should ship with targeted shared-suite coverage:

- routing explanation, session affinity, and policy evidence
- validation routes and effective-configuration preview
- request-history search, summary, redaction, and retention behavior
- observability metrics aggregation and export
- endpoint drain, resume, quarantine, and health-payload visibility
- compatibility behavior where new fields must default cleanly when older payloads omit them

When a feature spans the dashboard or SDKs, verify those surfaces too:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj
dotnet build src/Conductor.sln
cd dashboard && npm run build
cd sdk/javascript && npm test
cd sdk/python && set PYTHONPATH=src && python -m unittest discover -s tests
```

Example:

```csharp
public class MyComponentTests
{
    public void Value_DefaultsCorrectly()
    {
        MyComponent component = new MyComponent();
        component.Value.Should().Be(42);
    }

    public async Task Create_WithInvalidInput_Throws()
    {
        Func<Task> act = async () => await _Controller.Create(null);
        await act.Should().ThrowAsync<Exception>();
    }
}
```

## Packages

| Project | Key Packages |
|---|---|
| `Test.Shared` | `Touchstone.Core`, `FluentAssertions` |
| `Test.Automated` | `Touchstone.Cli` |
| `Test.Xunit` | `Touchstone.XunitAdapter`, `xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` |
| `Test.Nunit` | `Touchstone.NunitAdapter`, `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` |

## Notes

- The console runner is the fastest way to inspect raw descriptor output locally.
- The xUnit and NUnit hosts should remain thin; do not move real test logic out of `Test.Shared`.
- If a shared test starts failing in one framework host and not the other, treat that as a runner integration issue first, not as a reason to fork the test logic.
