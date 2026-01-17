# Conductor Testing Guide

This document describes how to run, understand, and extend the automated test suite for Conductor.

## Quick Start

### Running All Tests

```bash
# From the repository root
dotnet test src/Conductor.sln
```

### Running Tests with Verbose Output

```bash
dotnet test src/Conductor.sln --verbosity normal
```

### Running Tests with Code Coverage

```bash
dotnet test src/Conductor.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Coverage reports are generated in Cobertura XML format in the `TestResults` directory.

---

## Test Projects

The solution contains two test projects:

| Project | Location | Tests | Description |
|---------|----------|-------|-------------|
| `Conductor.Core.Tests` | `src/Conductor.Core.Tests/` | 516 | Unit tests for the Core library |
| `Conductor.Server.Tests` | `src/Conductor.Server.Tests/` | 72 | Unit tests for Server components |
| **Total** | | **588** | |

### Running Individual Test Projects

```bash
# Core tests only
dotnet test src/Conductor.Core.Tests/Conductor.Core.Tests.csproj

# Server tests only
dotnet test src/Conductor.Server.Tests/Conductor.Server.Tests.csproj
```

---

## Test Framework and Dependencies

Both test projects use the following packages:

| Package | Version | Purpose |
|---------|---------|---------|
| `xunit` | 2.9.0 | Test framework |
| `xunit.runner.visualstudio` | 3.0.0 | Visual Studio test adapter |
| `FluentAssertions` | 7.0.0 | Fluent assertion syntax |
| `Moq` | 4.20.72 | Mocking framework |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | .NET test SDK |
| `coverlet.collector` | 6.0.2 | Code coverage collection |

---

## Test Organization

### Conductor.Core.Tests Structure

```
src/Conductor.Core.Tests/
├── Models/
│   ├── VirtualModelRunnerTests.cs      # VMR validation, defaults, serialization
│   ├── ModelRunnerEndpointTests.cs     # Endpoint validation, URL construction
│   ├── ModelConfigurationTests.cs      # Config pinning, case-insensitivity
│   ├── ModelDefinitionTests.cs         # Definition validation, defaults
│   ├── CredentialTests.cs              # Token generation, validation
│   ├── AdministratorTests.cs           # Password hashing, email normalization
│   ├── UserMasterTests.cs              # User validation, redaction
│   ├── TenantMetadataTests.cs          # Tenant validation, JSON serialization
│   ├── UrlContextTests.cs              # URL parsing, request type detection
│   ├── EndpointHealthStateTests.cs     # Health state, copy semantics
│   ├── EndpointHealthStatusTests.cs    # Health status, uptime calculation
│   ├── VirtualModelRunnerHealthStatusTests.cs  # VMR health aggregation
│   ├── EndpointAvailabilityTests.cs    # Availability state
│   ├── EnumerationRequestTests.cs      # Pagination, filtering
│   ├── EnumerationResultTests.cs       # Result handling
│   └── ApiErrorResponseTests.cs        # Error response factory methods
├── Helpers/
│   ├── IdGeneratorTests.cs             # K-sortable ID generation
│   └── DataTableHelperTests.cs         # DataRow/DataTable parsing
├── Settings/
│   ├── CorsSettingsTests.cs            # CORS configuration
│   └── ServerSettingsTests.cs          # Server configuration
└── Enums/
    └── EnumTests.cs                    # Enum value verification
```

### Conductor.Server.Tests Structure

```
src/Conductor.Server.Tests/
└── Services/
    ├── AuthenticationServiceTests.cs   # Auth result classes
    └── HealthCheckServiceTests.cs      # Health state management
```

---

## Test Categories

### Model Validation Tests

Tests that verify model property validation, default values, and null handling:

```csharp
[Fact]
public void TenantId_WhenNull_ThrowsArgumentNullException()
{
    VirtualModelRunner vmr = new VirtualModelRunner();
    Action act = () => vmr.TenantId = null;
    act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
}
```

### Clamping and Range Tests

Tests that verify values are clamped to valid ranges:

```csharp
[Fact]
public void Port_WhenNegative_DefaultsTo11434()
{
    ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
    endpoint.Port = -1;
    endpoint.Port.Should().Be(11434);
}

[Fact]
public void MaxAgeSeconds_WhenOverMax_ClampsTo86400()
{
    CorsSettings cors = new CorsSettings();
    cors.MaxAgeSeconds = 100000;
    cors.MaxAgeSeconds.Should().Be(86400);
}
```

### Serialization Tests

Tests that verify JSON round-trip serialization:

```csharp
[Fact]
public void ModelRunnerEndpointIds_SerializesAndDeserializesCorrectly()
{
    VirtualModelRunner vmr = new VirtualModelRunner();
    vmr.ModelRunnerEndpointIds = new List<string> { "mre_1", "mre_2" };

    string json = vmr.ModelRunnerEndpointIdsJson;
    vmr.ModelRunnerEndpointIdsJson = json;

    vmr.ModelRunnerEndpointIds.Should().Contain("mre_1");
}
```

### Factory Method Tests

Tests for `FromDataRow` and `FromDataTable` methods:

```csharp
[Fact]
public void FromDataRow_WithNullRow_ReturnsNull()
{
    TenantMetadata result = TenantMetadata.FromDataRow(null);
    result.Should().BeNull();
}
```

### ID Generation Tests

Tests for K-sortable ID generation:

```csharp
[Fact]
public void GeneratedIds_AreKSortable()
{
    List<string> ids = new List<string>();
    for (int i = 0; i < 100; i++)
    {
        ids.Add(IdGenerator.NewVirtualModelRunnerId());
        Thread.Sleep(1);
    }

    List<string> sorted = ids.OrderBy(x => x).ToList();
    ids.Should().Equal(sorted);
}
```

---

## Running Specific Tests

### By Test Name Pattern

```bash
# Run tests matching a pattern
dotnet test src/Conductor.sln --filter "FullyQualifiedName~VirtualModelRunner"

# Run a specific test
dotnet test src/Conductor.sln --filter "FullyQualifiedName=Conductor.Core.Tests.Models.VirtualModelRunnerTests.TenantId_WhenNull_ThrowsArgumentNullException"
```

### By Test Class

```bash
dotnet test src/Conductor.sln --filter "ClassName=VirtualModelRunnerTests"
```

### By Namespace

```bash
dotnet test src/Conductor.sln --filter "Namespace~Conductor.Core.Tests.Models"
```

---

## Writing New Tests

### Test File Naming Convention

- Test files should be named `{ClassName}Tests.cs`
- Place in the appropriate subdirectory matching the source structure

### Test Method Naming Convention

Use the pattern: `{Method/Property}_{Scenario}_{ExpectedResult}`

Examples:
- `TenantId_WhenNull_ThrowsArgumentNullException`
- `Port_WhenNegative_DefaultsTo11434`
- `FromDataRow_WithValidData_CreatesInstance`

### Test Structure

Follow the Arrange-Act-Assert pattern:

```csharp
[Fact]
public void PropertyName_Scenario_ExpectedResult()
{
    // Arrange
    MyClass instance = new MyClass();

    // Act
    instance.Property = someValue;

    // Assert
    instance.Property.Should().Be(expectedValue);
}
```

### Using FluentAssertions

```csharp
// Basic assertions
result.Should().Be(expected);
result.Should().NotBeNull();
result.Should().BeTrue();

// Collection assertions
list.Should().HaveCount(3);
list.Should().Contain("item");
list.Should().BeEmpty();

// Exception assertions
Action act = () => obj.Method();
act.Should().Throw<ArgumentNullException>()
   .WithParameterName("paramName");

// DateTime assertions
timestamp.Should().BeOnOrAfter(before);
timestamp.Should().BeOnOrBefore(after);
```

### Using Regions

Organize tests using regions for readability:

```csharp
public class MyClassTests
{
    #region Default-Value-Tests

    [Fact]
    public void Property_DefaultsToExpectedValue() { }

    #endregion

    #region Validation-Tests

    [Fact]
    public void Property_WhenInvalid_ThrowsException() { }

    #endregion
}
```

---

## Known Limitations

### DatabaseDriverBase Mocking

The `DatabaseDriverBase` class has non-virtual properties (`Credential`, `User`, `Tenant`, `Administrator`, `ModelRunnerEndpoint`) that cannot be mocked with Moq. This limits unit testing of services that depend on the database.

**Affected Components:**
- `AuthenticationService` - Cannot mock database lookups
- `HealthCheckService` - Cannot mock endpoint enumeration

**Workaround:** Tests for these services focus on:
1. Constructor validation
2. Result class behavior (e.g., `AuthenticationResult`, `AdminAuthenticationResult`)
3. Pure functions that don't require database access

**Future Improvement:** Consider making `DatabaseDriverBase` properties virtual or introducing interfaces to enable full unit testing.

### WatsonWebserver/SwiftStack Types

The `HttpContextBase` and related types from WatsonWebserver (via SwiftStack) are difficult to mock. Full authentication flow testing requires integration tests with actual HTTP requests.

---

## Integration Testing

Integration tests are not yet implemented but would cover:

1. **Database Integration** - CRUD operations against SQLite, PostgreSQL, SQL Server, MySQL
2. **API Integration** - Full HTTP request/response cycles
3. **Authentication Flows** - Bearer token and header-based authentication
4. **Proxy Functionality** - Request routing through virtual model runners

### Setting Up Integration Tests

For future integration tests, consider:

```csharp
public class DatabaseIntegrationTests : IDisposable
{
    private readonly DatabaseDriverBase _Database;

    public DatabaseIntegrationTests()
    {
        // Create in-memory SQLite database
        _Database = new SqliteDatabaseDriver(":memory:");
    }

    public void Dispose()
    {
        _Database?.Dispose();
    }

    [Fact]
    public async Task CreateAndRead_Tenant_RoundTrips()
    {
        // Test implementation
    }
}
```

---

## Continuous Integration

### GitHub Actions Example

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: Restore dependencies
      run: dotnet restore src/Conductor.sln

    - name: Build
      run: dotnet build src/Conductor.sln --no-restore

    - name: Test
      run: dotnet test src/Conductor.sln --no-build --verbosity normal

    - name: Test with Coverage
      run: dotnet test src/Conductor.sln --no-build --collect:"XPlat Code Coverage"
```

---

## Test Results Summary

As of the last test run:

```
Test Run Successful.
Total tests: 588
     Passed: 588
     Failed: 0
     Skipped: 0
Duration: ~5 seconds
```

### Test Distribution

| Category | Count |
|----------|-------|
| Model Tests | ~350 |
| Helper Tests | ~50 |
| Settings Tests | ~30 |
| Enum Tests | ~25 |
| URL Parsing Tests | ~35 |
| Service Result Tests | ~98 |

---

## Troubleshooting

### Tests Not Discovered

Ensure the test project references are correct:

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
```

### Build Errors in Tests

Run a clean build:

```bash
dotnet clean src/Conductor.sln
dotnet build src/Conductor.sln
dotnet test src/Conductor.sln
```

### Coverage Not Generated

Ensure coverlet.collector is referenced:

```xml
<PackageReference Include="coverlet.collector" Version="6.0.2" />
```

---

## Contributing

When adding new tests:

1. Follow existing naming conventions
2. Use FluentAssertions for all assertions
3. Organize tests using regions
4. Include both positive and negative test cases
5. Test edge cases (null, empty, boundary values)
6. Run all tests before submitting a PR

```bash
# Verify all tests pass
dotnet test src/Conductor.sln

# Check for warnings
dotnet build src/Conductor.sln --warnaserror
```
