---
title: Keyboard Shortcuts
description: All keyboard shortcuts for navigating dotsider.
---

## Global

| Key | Action |
|-----|--------|
| `1`–`8` | Switch tabs |
| `Enter` | Drill into selected item |
| `Esc` | Go back (when no search or modal is active) |
| `/` | Search |
| `n` / `N` | Next / previous search match |
| `y` | Yank (copy) — selected text in editors, or focused row in tables |
| `yy` | Yank entire line under cursor |
| `V` | Select entire line under cursor |
| `iw` | Select inner word under cursor (letters, digits, underscores) |
| `iW` | Select inner WORD under cursor (whitespace-delimited) |
| `yiw` | Select + yank inner word in one motion |
| `yiW` | Select + yank inner WORD in one motion |
| `Tab` | Cycle focus between info panels and tables |
| `s` | Toggle human-readable sizes |
| `q` | Quit |

## PE / Metadata tab

| Key | Action |
|-----|--------|
| `Tab` | Cycle focus: PE Headers → CLR Header → metadata table |
| `g` | Jump to tab 3's IL view for focused TypeDef/MethodDef |

## Tab 3: IL / Native

| Key | Action |
|-----|--------|
| `Enter` / `gd` | Go to definition (on a token-bearing instruction) |
| `Esc` | Go back from go-to-definition |
| `x` | Jump to method body in Hex Dump |
| `o` | Open embedded source for the selected method, when available |
| `u` | Copy Source Link URL from a `[source link]` marker |
| `l` | Focus the IL disassembly editor |

## Size Map tab

| Key | Action |
|-----|--------|
| `←` / `→` | Move selection across the current level |
| `Enter` / click | Drill into a region; on a managed method leaf, jump to its IL |
| `Esc` / right-click | Go up one level |
| `w` | Why is this in the binary — dependency chain popup (Native AOT with DGML sidecar) |
| `s` | Toggle size formatting |

## Hex Dump tab — Normal mode

| Key | Action |
|-----|--------|
| `h` `j` `k` `l` | Vim-style cursor movement |
| `g` | Jump to hex offset |
| `i` | Enter insert mode |
| `e` | Toggle endianness (LE/BE) |
| `Tab` | Toggle focus between hex editor and data interpretation panel |
| `Ctrl+T` | Toggle text/hex search |
| `Ctrl+S` | Save modified bytes |

## Hex Dump tab — Insert mode

| Key | Action |
|-----|--------|
| `0`–`9`, `a`–`f` | Overwrite byte (two digits per byte) |
| `Esc` | Return to normal mode |

## Diff mode

| Key | Action |
|-----|--------|
| `1`–`4` / `←` `→` | Switch diff tabs |
| `f` | Cycle filters (All / Added / Removed / Changed) |
| `y` | Yank focused row or selected text |
| `Tab` | Cycle focus between summary panels |

## Size-diff mode

| Key | Action |
|-----|--------|
| `1`–`2` | Switch tabs (Summary / Size Map) |
| `f` | Cycle direction filters (All / Added / Removed / Grown / Shrunk) |
| `Enter` / click | Drill into the selected subtree |
| `Esc` / right-click | Go up one level (dismisses popups and search first) |
| `←` `→` | Cycle rectangle selection |
| `/` then `n` `N` | Search the current level, jump between matches |
| `w` | Why is this in the binary — dependency chain; aggregate tiles show representative child chains; press again on a changed entry to flip sides |
| `d` | Disassemble the native body (binary-backed pairs); repeated presses cycle an aggregate's symbols and, for changed entries, both builds' bodies |
| `y` | Yank selected text in the summary or popups |

## NuGet mode

| Key | Action |
|-----|--------|
| `Enter` | Open selected DLL in the full analyzer |
| `Esc` | Return to package browser from DLL inspector |
| `Tab` | Cycle focus between Package Info and DLL table |
| `y` | Yank focused DLL path or selected text |
