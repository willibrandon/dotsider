# Issue #229 test research

## Bounded target inventory

- Production target: `src/Dotsider.Core/Analysis/MstatReader.cs`, specifically
  `ReadDeduplicatedMethods`.
- Supporting cursor: `src/Dotsider.Core/Analysis/IlCursor.cs`, including the unread-byte
  count used to reject impossible nested counts.
- Existing paired tests: `tests/Dotsider.Tests/MstatReaderTests.cs`.
- New fixture target: a deterministic in-process format-2.2 managed PE generated in
  `tests/Dotsider.Tests`, with the same global-method IL and `.names` section shape as the
  runtime writer.
- New regression target: a focused sealed MSTest 4 class in
  `tests/Dotsider.Tests/MstatDeduplicatedMethodBoundsTests.cs`.

The one-time Roslyn static-pairing scan classified `MstatReader.cs` as paired with
`MstatReaderTests.cs`. This is a static symbol-pairing heuristic, not line or branch coverage.

## Runtime writer contract

`D:\SRC\runtime\src\coreclr\tools\aot\ILCompiler.Compiler\Compiler\MstatObjectDumper.cs`
defines mstat format 2.2 and emits each `DeduplicatedMethods` row as:

1. `ldtoken` for the original method.
2. `ldc.i4` for the number of folded targets.
3. For each target, `ldtoken` for the target method and `ldc.i4` for its byte offset in
   the `.names` section.

The writer serializes node names as length-prefixed UTF-8 strings and places them in a
read-only `.names` PE section. It also emits the independent `Methods` and `Types` global
method streams.

## Existing conventions

- `MSTest.Sdk` 4.3.0 on `net10.0`, run by Microsoft Testing Platform.
- Public sealed `[TestClass]` types and public `[TestMethod]` methods.
- Exact MSTest 4 assertions such as `Assert.HasCount`, `Assert.IsEmpty`,
  `Assert.AreEqual`, and comparison assertions.
- Alphabetical using directives, one declared type per file, and XML documentation for
  public types and members and public members exposed by internal types.
- Deterministic, in-process synthetic metadata is established practice.
- Tests must not add subprocess infrastructure, packages, timeout/parallelization changes,
  or production changes.

## Acceptance checklist

- [ ] Build a deterministic synthetic format-2.2 mstat matching the runtime writer layout.
- [ ] Include a real `.names` PE section and assert decoded target node names.
- [ ] Cover valid zero, one, and multiple folded-target counts.
- [ ] Cover negative, impossible, and `int.MaxValue` counts.
- [ ] Cover a truncated count, a truncated pair, and malformed target-token/name operands.
- [ ] Prove completed dedup rows survive later damage.
- [ ] Prove a partially decoded current row is atomic and omitted.
- [ ] Prove independent `Methods` and `Types` sections survive dedup-stream damage.
- [ ] Add a robust allocation regression using
  `GC.GetAllocatedBytesForCurrentThread`.
- [ ] Warm both inputs, take five repeated measurements, compare minima, and verify a
  count of 2,000,000 stays within 1 MiB of the zero-count baseline.
- [ ] Keep every change confined to tests and `.testagent`.
- [ ] Build and run the narrow test class, then perform final workspace-level validation.
- [ ] Perform pseudo-mutation and assertion-quality reviews before completion.
