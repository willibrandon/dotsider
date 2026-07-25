# Issue #229 test status

- Research: complete.
- Plan: complete.
- Implementation: complete.
- Narrow build: passed with
  `dotnet build tests/Dotsider.Tests/Dotsider.Tests.csproj --no-restore`
  (0 warnings, 0 errors).
- Narrow test: passed with
  `dotnet test --project tests/Dotsider.Tests/Dotsider.Tests.csproj --no-build --filter "FullyQualifiedName~Dotsider.Tests.MstatDeduplicatedMethodBoundsTests"`
  (12 passed, 0 failed).
- Focused Debug validation: 35 passed, 0 failed
  (`MstatDeduplicatedMethodBoundsTests`, `MstatReaderTests`, and
  `MstatLocatorTests`).
- Focused Release validation: 35 passed, 0 failed over the same scope.
- Full Debug validation: 3,077 passed, 0 failed, 23 skipped.
- Full Release validation: 3,077 passed, 0 failed, 23 skipped.
- Full Release solution build: passed with 0 warnings and 0 errors.
- Linux x64 focused validation: 12 passed, 0 failed.
- macOS arm64 focused validation: 12 passed, 0 failed.
- API generation: 1,659 API items loaded and 227 Markdown files generated with
  0 warnings and 0 errors.
- Documentation site build: passed.
- Allocation audit: every `List<T>` in `MstatReader` now uses a capacity-free
  constructor; no untrusted stream count controls a list capacity.
- `git diff --check`: passed.

## Pseudo-mutation review

- Removing the negative-count guard is killed by
  `ReadDeduplicatedMethods_InvalidCount_OmitsCurrentRowAndPreservesOtherSections`: the
  malformed row must not appear and the public facade must still return a report.
- Removing the remaining-byte guard is killed functionally by the impossible-count cases
  and by the 2,000,000-count allocation comparison.
- Changing the exact-fit comparison from `>` to `>=`, or increasing the six-byte minimum,
  is killed by `ReadDeduplicatedMethods_ExactCount_ReturnsNamedTarget`; its target name is
  at `.names` offset zero, so the pair is exactly five token bytes plus one `ldc.i4.0`.
- Reintroducing `new List<string>(count)` is killed by
  `ReadDeduplicatedMethods_InflatedCount_DoesNotAllocateFromDeclaredCount`: the old path
  reserves roughly 16 MiB, beyond the 1 MiB baseline tolerance.
- Replacing malformed-pair early returns with partial-row publication is killed by the
  truncated/malformed prefix tests and
  `ReadDeduplicatedMethods_MalformedTargetNameOffset_OmitsPartialCurrentRow`.
- Treating an unavailable `.names` offset as a decoded target is killed by
  `ReadDeduplicatedMethods_OutOfRangeTargetNameOffset_KeepsRowWithoutTarget`.
- No high-risk survived mutation remains in the bounded count/allocation behavior. Lowering
  the conservative six-byte precheck to five can reach the same cursor failure one step
  later without changing externally visible results; it is behaviorally equivalent for
  these no-capacity-allocation loops.

## Assertion-quality review

- Ten test methods produce twelve cases and all contain meaningful assertions.
- No test is assertion-free, trivial-only, self-referential, or dependent on an exception
  escaping from the lenient public facade.
- Equality assertions pin format, names, ordering, sizes, and section identity; collection
  assertions pin row atomicity, completed-prefix retention, and omitted targets; the
  comparison assertion pins the allocation invariant.
- The malformed cases assert both the damaged section and independent-section state, so a
  broad fallback to null/empty cannot satisfy them.
- Exception assertions are intentionally absent because `MstatReader` promises malformed
  input does not throw.
