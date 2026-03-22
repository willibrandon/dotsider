---
title: Keyboard Shortcuts
description: All keyboard shortcuts for navigating dotsider.
---

## Global

| Key | Action |
|-----|--------|
| `1`–`8` | Switch tabs |
| `Enter` | Drill into selected item |
| `Backspace` | Go back |
| `/` | Search |
| `n` / `N` | Next / previous search match |
| `y` | Yank (copy) — selected text in editors, or focused row in tables |
| `Tab` | Cycle focus between info panels and tables |
| `s` | Toggle human-readable sizes |
| `q` | Quit |

## PE / Metadata tab

| Key | Action |
|-----|--------|
| `Tab` | Cycle focus: PE Headers → CLR Header → metadata table |
| `g` | Jump to IL Inspector for focused TypeDef/MethodDef |

## IL Inspector tab

| Key | Action |
|-----|--------|
| `x` | Jump to method body in Hex Dump |
| `l` | Focus the IL disassembly editor |

## Hex Dump tab — Normal mode

| Key | Action |
|-----|--------|
| `h` `j` `k` `l` | Vim-style cursor movement |
| `g` | Jump to hex offset |
| `i` | Enter insert mode |
| `e` | Toggle endianness (LE/BE) |
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

## NuGet mode

| Key | Action |
|-----|--------|
| `Enter` | Open selected DLL in the full analyzer |
| `Esc` | Return to package browser from DLL inspector |
| `Tab` | Cycle focus between Package Info and DLL table |
| `y` | Yank focused DLL path or selected text |
