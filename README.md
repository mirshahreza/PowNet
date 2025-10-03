# PowNet

A modern, modular utility and extensions toolkit for .NET 10. This repository includes production-ready helpers for collections, diagnostics, configuration, caching, development tooling, and more – all covered by comprehensive unit tests.

## Features
- Rich `Collection` and `ConcurrentDictionary` extensions (batching, partitioning, safe parallel ops)
- `Debug`/validation helpers with development-only behaviours
- Lightweight diagnostics and performance measurement utilities
- Simple caching primitives and warm-up helpers
- Development tools for code generation and quick analysis
- Strongly-typed configuration helpers

## Documentation
- Full API reference: see [DOCS.md](./DOCS.md)

## Requirements
- .NET 10

## Getting Started
1) Reference the `PowNet` project from your application or tests.
2) Use extensions directly, for example:

```csharp
using PowNet.Extensions;

var dict = new ConcurrentDictionary<string, int>();
dict.UpdateMany(new[]
{
    new KeyValuePair<string, int>("a", 1),
    new KeyValuePair<string, int>("b", 2),
    new KeyValuePair<string, int>("a", 3)
});
// dict["a"] == 3, dict["b"] == 2
```

## Development
- Run tests: `dotnet test`
- Target framework: `.NET 10`

## Contributing
Issues and pull requests are welcome. Please include tests for new features and bug fixes.

## License
This project is licensed under the MIT License. See `LICENSE` for details.
