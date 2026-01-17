# Conductor - Claude Code Guidelines

## Project Overview

Conductor is a platform for managing models, model runners, model configurations, and virtualizing combinations into virtual model runners exposed to the network via OpenAI or Ollama compatible APIs.

## Code Style Guidelines

These must be followed STRICTLY:

### Namespace and Using Statements
- The namespace declaration should always be at the top, and using statements should be contained INSIDE the namespace block
- All Microsoft and standard system library usings should be first, in alphabetical order, followed by other using statements, in alphabetical order

### Documentation
- All public members, constructors, and public methods must have code documentation
- No code documentation should be applied to private members or private methods
- Document nullability in XML comments
- Document thread safety guarantees in XML comments
- Document which exceptions public methods can throw using `/// <exception>` tags
- Where appropriate, ensure code documentation outlines default values, minimum values, and maximum values

### Variable and Member Naming
- Private class member variable names must start with an underscore and then be Pascal cased (e.g., `_FooBar` not `_fooBar`)
- Do not use `var` when defining a variable. Use its actual type
- All public members should have explicit getters and setters using backing variables when value requires range or null validation

### Type Usage
- Do not use tuples unless absolutely necessary
- Limit each file to containing exactly one class or exactly one enum. Do not nest multiple classes or multiple enums in a single file

### Async/Await
- Async calls should use `.ConfigureAwait(false)` where appropriate
- Every async method should accept a CancellationToken as an input property, unless the class has a CancellationToken as a class member or a CancellationTokenSource as a class member
- Async calls should check whether or not cancellation has been requested at appropriate places
- When implementing a method that returns an IEnumerable, also create an async variant of that same method that includes a CancellationToken

### Exception Handling
- Use specific exception types rather than generic Exception
- Always include meaningful error messages with context
- Consider using custom exception types for domain-specific errors
- Use exception filters when appropriate: `catch (SqlException ex) when (ex.Number == 2601)`

### Resource Management
- Implement IDisposable/IAsyncDisposable when holding unmanaged resources or disposable objects
- Use `using` statements or `using` declarations for IDisposable objects
- Follow the full Dispose pattern with `protected virtual void Dispose(bool disposing)`
- Always call `base.Dispose()` in derived classes

### Null Safety
- Use nullable reference types (enable `<Nullable>enable</Nullable>` in project files)
- Validate input parameters with guard clauses at method start
- Use `ArgumentNullException.ThrowIfNull()` for .NET 6+ or manual null checks
- Proactively identify and eliminate any situations in code where null might cause exceptions to be thrown

### Thread Safety
- Use Interlocked operations for simple atomic operations
- Prefer ReaderWriterLockSlim over lock for read-heavy scenarios

### LINQ
- Prefer LINQ methods over manual loops when readability is not compromised
- Use `.Any()` instead of `.Count() > 0` for existence checks
- Be aware of multiple enumeration issues - consider `.ToList()` when needed
- Use `.FirstOrDefault()` with null checks rather than `.First()` when element might not exist

### Configuration
- Avoid using constant values for things that a developer may later want to configure or otherwise change
- Instead use a public member with a backing private member set to a reasonable default

### General
- If my code uses manually prepared strings for SQL statements, there is probably a very good reason for it
- If a README exists, analyze it and ensure it is accurate
- Compile the code and ensure it is free of errors and warnings
- Ensure NO Console.WriteLine statements are added to library code
- Regions for Public-Members, Private-Members, Constructors-and-Factories, Public-Methods, and Private-Methods are NOT required for small files under 500 lines

## Build Commands

```bash
# Build the server
cd src/Conductor.Server
dotnet build

# Run the server
cd src/Conductor.Server
dotnet run

# Build and run the dashboard
cd dashboard
npm install
npm run dev
```

## Project Structure

- `src/Conductor.Core/` - Core models, database implementations, and shared logic
- `src/Conductor.Server/` - ASP.NET server with REST API and proxy controllers
- `dashboard/` - React dashboard application
- `docker/` - Docker Compose configuration

## Database Support

- SQLite (default)
- PostgreSQL
- SQL Server
- MySQL

All database implementations are in `src/Conductor.Core/Database/[Provider]/`.
