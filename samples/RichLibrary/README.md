# RichLibrary

Feature-rich class library (v2.5.1) used as the baseline for analysis and diff testing. Generates a `.nupkg` on build for NuGet analysis tests.

- Generic `IRepository<T>` interface with CRUD operations
- Domain records: `User` (with validation attributes), `Product`, `UserRole` enum
- Services: `UserService`, `ProductCatalog` (with `AggressiveInlining`)
- Dual JSON serialization: `Newtonsoft.Json` and `System.Text.Json`
- String extension methods: `Truncate`, `ComputeHash`, `ToTitleCase`
- Dependency: Newtonsoft.Json 13.0.3
