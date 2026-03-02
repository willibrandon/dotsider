"dotsider" — Analyze .NET assemblies like a boss

  A TUI for inspecting .NET DLLs/EXEs the way binsider inspects ELF binaries. .NET has fantastic built-in APIs for this (System.Reflection.Metadata, System.Reflection.PortableExecutable), so you wouldn't even need many third-party dependencies.

  Tab layout (mirroring binsider's 5-tab design):

  1. General
  - Assembly name, version, target framework, culture, public key token
  - File size, timestamps, architecture (AnyCPU/x64/ARM64)
  - Dependency table — assembly references + resolved paths (the .NET equivalent of ldd). Press Enter to drill into a referenced assembly, Backspace to go back up the chain.
  - NuGet package origin detection (which package brought this DLL in)

  2. PE / Metadata Structure (analogous to binsider's "Static Analysis")
  - Split pane top: PE Headers (DOS, COFF, Optional Header, Data Directories) | CLR Header (runtime version, metadata RVA, flags like ILOnly/32BitRequired/StrongNameSigned)
  - Sub-tabs at bottom (like binsider's 6 sub-tables):
    - Sections — .text, .rsrc, .reloc with VirtualAddress, SizeOfRawData, Characteristics
    - TypeDef — all types with namespace, name, flags, base type
    - MethodDef — name, signature, RVA, ImplFlags, Flags
    - MemberRef / TypeRef / AssemblyRef — external references
    - Custom Attributes — attribute type + decoded constructor args
    - Resources — embedded resources with size and type
  - Search with /, human-readable sizes toggle with s, Enter for detail popup

  3. IL Inspector (binsider's dynamic analysis equivalent, but static)
  - Tree view of Namespace → Type → Method
  - Select a method → see IL disassembly in a scrollable pane (using System.Reflection.Metadata to decode IL opcodes)
  - Optional: integrate ICSharpCode.Decompiler to show decompiled C# side-by-side
  - Search across all method bodies for specific IL patterns or string references

  4. Strings
  - Extract from the #US (User Strings) heap — all string literals in the assembly
  - Extract from the #Strings heap — metadata names
  - Extract raw printable strings from the binary (like binsider's strings(1))
  - +/- to adjust minimum length, / to search, Enter for detail popup with offset

  5. Hex Dump
  - Full hex editor view using hex1b's Editor widget with HexEditorViewRenderer
  - Jump to offset (g), search bytes (/), section-aware navigation
  - You already have this widget built into hex1b!

  Why hex1b is perfect for this

  You already have every widget binsider uses, and then some:

  ┌───────────────────────┬────────────────────────────────────────────┐
  │   Binsider feature    │              hex1b equivalent              │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Tab bar               │ TabPanel                                   │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Split panes           │ Splitter / HSplitter                       │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Scrollable tables     │ Table with virtualized data source         │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Detail popup          │ WindowPanel (floating windows)             │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Tree navigation       │ Tree widget                                │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Hex dump              │ Editor + HexEditorViewRenderer (built-in!) │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Search input          │ TextBox + filtering                        │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Key binding hints bar │ InfoBar                                    │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Dependency drill-down │ Navigator (push/pop routes)                │
  ├───────────────────────┼────────────────────────────────────────────┤
  │ Loading spinner       │ Spinner / Progress (indeterminate)         │
  └───────────────────────┴────────────────────────────────────────────┘

  Plus you get things binsider doesn't have: charts for visualizing section sizes (BreakdownChart, ColumnChart), sixel for rendering embedded image resources, drag-and-drop for rearranging views, and the Responsive widget for adapting layout to terminal size.

  Key .NET APIs (no heavy dependencies needed)

  - System.Reflection.Metadata + System.Reflection.PortableExecutable — ships with the runtime, gives you full PE + .NET metadata table access
  - System.Reflection.Metadata.Ecma335 — low-level metadata table readers for all 45 metadata tables
  - ICSharpCode.Decompiler (optional) — ILSpy's engine for C# decompilation

  Bonus ideas to go beyond binsider's model

  - Diff mode: load two versions of an assembly side-by-side and highlight API changes (added/removed types, changed signatures)
  - NuGet package mode: open a .nupkg directly, browse its contents, inspect every DLL inside
  - Dependency graph visualization: use the Surface widget or braille canvas to render the assembly reference graph
  - Size treemap: visualize which types/methods contribute most to assembly size (useful for trimming)

  ---

This is probably the most natural 1:1 port of the binsider concept into .NET, and it fills a real gap — there's no good TUI assembly inspector in the dotnet ecosystem today. Tools like ILSpy and dnSpy are GUI-only.
