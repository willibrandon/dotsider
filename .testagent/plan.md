# Issue #229 test plan

## Phase 1: Synthetic format-2.2 fixture

- Add `SyntheticMstat22PeBuilder.cs` to serialize the custom `.names` PE section.
- Add `SyntheticMstat22Fault.cs` to name precise malformed-stream variants.
- Add `SyntheticMstat22Builder.cs` to emit deterministic metadata, method references,
  global `Methods`, `Types`, and `DeduplicatedMethods` IL bodies, and serialized names.
- Validate the fixture through the public `MstatReader.Read(Stream)` facade.

## Phase 2: Functional boundaries and recovery

- `ReadDeduplicatedMethods_ZeroCount_ReturnsEmptyTargetSet` covers zero.
- `ReadDeduplicatedMethods_ExactCount_ReturnsNamedTarget` covers one exact pair and
  `.names` resolution using the runtime writer's exact six-byte minimum pair encoding.
- `ReadDeduplicatedMethods_MultipleCount_ReturnsEveryNamedTargetInOrder` covers multiple
  pairs and ordering.
- `ReadDeduplicatedMethods_InvalidCount_OmitsCurrentRowAndPreservesOtherSections` covers
  negative, impossible, and `int.MaxValue` count values while asserting the independent
  method/type entries remain exact.
- `ReadDeduplicatedMethods_TruncatedCount_OmitsCurrentRowAndPreservesOtherSections`
  covers a physically truncated `ldc.i4` operand.
- `ReadDeduplicatedMethods_TruncatedPair_KeepsCompletedPrefix` covers a later row whose
  target pair ends after `ldtoken`, preserving an earlier complete row.
- `ReadDeduplicatedMethods_MalformedTargetToken_KeepsCompletedPrefix` covers an invalid
  target-token opcode.
- `ReadDeduplicatedMethods_MalformedTargetNameOffset_OmitsPartialCurrentRow` lets one
  target in the current row decode before the next name operand fails, proving the current
  row is atomic and the completed prefix remains.
- `ReadDeduplicatedMethods_OutOfRangeTargetNameOffset_KeepsRowWithoutTarget` covers a
  structurally complete pair whose serialized-name offset cannot be resolved.

## Phase 3: Allocation regression

- `ReadDeduplicatedMethods_InflatedCount_DoesNotAllocateFromDeclaredCount` builds
  zero-count and 2,000,000-count tiny images once, warms both reads, records five
  current-thread allocation measurements for each, compares the minima, and requires the
  hostile input to remain within 1 MiB of baseline. The old capacity-sized list reserves
  roughly 16 MiB and therefore fails without using an unsafe `int.MaxValue` measurement.

## Phase 4: Validation and review

- Build `tests/Dotsider.Tests/Dotsider.Tests.csproj`.
- Run the new class with the repository's Microsoft Testing Platform filter syntax.
- Run final workspace validation once.
- Re-open every generated test and map every acceptance item to its exact assertion.
- Perform pseudo-mutation analysis on count sign/boundary, minimum pair size, early-return,
  and allocation mutations.
- Classify all assertions and record gap/assertion findings and any fixes in
  `.testagent/status.md`.
