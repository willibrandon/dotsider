---
title: Dep Graph
description: Full transitive assembly dependency graph with TypeRef-weighted edges.
---

![Dep Graph tab](../../../assets/screenshots/dep-graph.png)

The **Dep Graph** tab (`6`) renders the full transitive closure of assembly references rooted at the analyzed assembly:

- Your assembly sits at the root at depth 0
- Every referenced assembly appears as a node, positioned by BFS depth
- Edge weights show how many TypeRefs in the referencing assembly resolve to the exact full identity of the target
- Framework assemblies (BCL, runtime pack) are included by default

Identity is keyed on the full `(name, version, culture, public key token)` tuple, so two assemblies that share a simple name but differ in any identity field appear as two distinct nodes. When a simple name collides, labels show only the identity fields that actually differ — for example `TargetLib v2.0.0.0` when only versions differ, or `TargetLib v1.0.0.0 (b77a5c56)` when only public key tokens differ.

## Unresolved and identity-mismatched nodes

References that cannot be located on disk or inside a bundle render as leaf nodes prefixed with `?`. When a probe produces a file whose simple name matches but whose manifest identity does not, the node renders with a `!` prefix and the graph does not expand from the mismatched file — the closure stays honest about what it actually contains. A `↪` prefix marks a node where binding policy rewrote the requested version; a `×` prefix marks a configured `<codeBase>` whose `href` does not exist on disk.

## Native AOT binaries

ILC folds every managed assembly into the image, so a Native AOT binary carries no AssemblyRef table to walk. With the mstat and DGML sidecars next to the binary (see [Size Map](/usage/size-map/#native-aot-binaries) for the publish properties), the graph shows what the compiler saw instead: one node per compiled-in assembly, edges aggregated from the compiler's dependency graph and weighted by how many dependencies cross each assembly pair, and the binary's native import modules (`kernel32.dll` and friends) at depth 1 weighted by imported function count.

Assembly nodes keep their full identity, so `f` filters framework assemblies as usual. `Enter` explains that the assembly was compiled into the native image — there is no separate file to open. Without sidecars the graph shows the root plus its native imports, the only dependency facts the binary itself records.

## .NET Framework targets

For a .NET Framework root, resolution walks the framework binder rather than the .NET Core probe chain: framework unification, `app.config` and `machine.config` `<bindingRedirect>` entries, publisher-policy assemblies in the GAC, the GAC itself, the framework runtime directory, configured `<codeBase>` hrefs, then the application base and `<probing privatePath>`. Nodes are keyed on the bound identity, so two upstream references at different versions that both redirect to the same loaded version collapse onto a single node.

The binder switches paths and GAC-token format on the root's CLR generation:

- **CLR 4** (net40 through net48) — `%WINDIR%\Microsoft.NET\assembly\GAC_*` with `v4.0_<version>__<pkt>` tokens; runtime directory `v4.0.30319`.
- **CLR 2** (.NET Framework 2.0 / 3.0 / 3.5 SP1) — `%WINDIR%\assembly\{GAC_MSIL, GAC_<arch>, GAC}` with no-prefix `<version>__<pkt>` tokens; runtime directory `v2.0.50727`. Detected from the `mscorlib` v2 reference because pre-4.0 assemblies don't carry a `TargetFrameworkAttribute`.

## Scope and filter

Two independent controls narrow what you see. Both default to off so the full transitive closure is the starting view.

- `d`: toggle **scope**. `all` (default) shows every node in the closure; `direct` shows only the root and its direct references — depth 1 — plus the edges between them. `direct` is the .NET-native way to think about a project's own package dependencies without the transitive expansion.
- `f`: toggle **framework filtering**. Assemblies classified as part of the .NET framework (shared runtime, runtime pack, or well-known Microsoft public key tokens) are hidden along with any edges that touch them. Deployment-model-agnostic — framework assemblies shipped inside a self-contained publish or single-file bundle are classified consistently with framework assemblies loaded from the shared runtime.

The controls compose. `scope: direct` with framework filtering on shows the root plus only the non-framework direct references, which is typically the clearest view of what a project actually brings into its dependency graph. The root is always visible regardless of either control. The status line reports visible counts versus totals and which controls are active.

Transitive-only is intentionally not an option — hiding direct parents would produce disconnected islands and remove the explanation path that shows why deeper nodes are in the closure.

## Navigation

- `←` / `→`: move the keyboard selection across visible nodes. Selection auto-scrolls the viewport to keep the selected box in view.
- `↑` / `↓`: scroll the viewport up or down one row at a time.
- `PageUp` / `PageDown`: scroll by one viewport height.
- `Home` / `End`: jump to the top or bottom of the graph.
- Mouse wheel over the graph scrolls the viewport vertically; the vertical scrollbar at the right edge can be dragged or track-clicked when the graph overflows.
- `Enter`: open the selected node's resolved assembly in a new analysis context. Uses the resolution location recorded at traversal time, not a fresh probe from the root, so transitive nodes open correctly. Enter on the root is a no-op; Enter on an unresolved or identity-mismatched leaf surfaces an explanatory status message.
- `Esc`: return to the prior analysis context.
- `/`: search by name; `n` jumps to the next match and `N` (shift + n) jumps to the previous. Search always operates on the visible model, so hidden nodes (by scope or filter) are never matched or selected.

When the graph fits the viewport, depth bands spread through the available height. When it doesn't, bands pack tightly from the top and scroll reveals the rest.
