# Dotsider.DocGenerator

Generates Starlight-compatible API reference markdown from the Dotsider.Core source code. Uses DocFX to extract metadata from XML doc comments, then converts the YAML output to markdown pages.

## Usage

```bash
dotnet run --project src/Dotsider.DocGenerator
```

No arguments required. The tool discovers the repo root automatically and writes to `docs/src/content/docs/api/`.

## Pipeline

1. **DocFX metadata** — runs DocFX against `Dotsider.Core.csproj` to produce YAML files in `_metadata/`
2. **YAML → Markdown** — converts each YAML file to a Starlight markdown page with frontmatter, syntax blocks, parameter tables, and cross-reference links

Hand-written `index.md` files in the output directory are preserved across regeneration.

## Key Classes

| Class | Description |
|-------|-------------|
| `Program` | Entry point — discovers repo root, orchestrates DocFX then conversion |
| `YamlToMarkdownConverter` | Parses DocFX YAML into `ApiItem` trees and emits Starlight markdown |
| `ApiItem` | In-memory model for an API element (namespace, type, method, property, etc.) |
| `ParameterItem` | Parameter name, type, and description from XML docs |

## Output Format

Each generated page includes:

- YAML frontmatter (`title`, `description`, `slug`)
- Namespace and assembly info
- C# syntax block
- Inheritance chain and implemented interfaces
- Member sections (constructors, properties, methods, fields, events)
- Parameters and return types
- Remarks and examples from XML doc comments
- Links to related types (internal cross-refs and Microsoft Learn)
