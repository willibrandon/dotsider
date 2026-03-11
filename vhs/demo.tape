# dotsider Quick Start Demo
Output assets/demo.gif

Set Shell bash
Set FontSize 14
Set Width 1400
Set Height 800
Set Padding 20
Set Theme "Catppuccin Mocha"
Set WindowBar Colorful
Set TypingSpeed 30ms

Hide
Type "export PATH=/tmp/dotsider-bin:$PATH"
Enter
Sleep 500ms
Type "clear"
Enter
Sleep 500ms
Show

# ── 1. Open an assembly ───────────────────────────────
# "Point dotsider at any .NET DLL or EXE"
# "You land on the General tab showing assembly identity,
#  target framework, architecture, and the dependency table."
Type "dotsider samples/HelloWorld/bin/Debug/net10.0/HelloWorld.dll"
Sleep 500ms
Enter
Sleep 5s

# ── 2. Navigate tabs ──────────────────────────────────
# "Press 1 through 8 to switch between tabs"
Type "1"
Sleep 1500ms

Type "2"
Sleep 1500ms

Type "3"
Sleep 1500ms

Type "4"
Sleep 1500ms

Type "5"
Sleep 1500ms

Type "6"
Sleep 1500ms

Type "7"
Sleep 1500ms

Type "8"
Sleep 1500ms

# ── 3. Drill in ──────────────────────────────────────
# "Press Enter on any highlighted item to drill deeper —
#  an assembly reference, a method in the IL tree,
#  or a dependency node in the graph."
# Go to General tab to drill into an assembly reference
Type "1"
Sleep 1500ms

# Enter to drill into the highlighted assembly reference
Enter
Sleep 2s

# "Press Backspace to go back."
Backspace
Sleep 1500ms

# ── 4. Search ─────────────────────────────────────────
# "Press / to search. Matches are highlighted inline.
#  Use n and N to jump between matches."
Type "/"
Sleep 500ms
Type "System"
Sleep 1500ms
Enter
Sleep 1s

Type "n"
Sleep 800ms
Type "n"
Sleep 800ms
Type "N"
Sleep 800ms

Escape
Sleep 1s

# Quit the analyzer
Type "q"
Sleep 1500ms

# ── 5. Compare two assemblies ─────────────────────────
# "dotsider diff v1.dll v2.dll"
# "Opens a side-by-side view."
Type "dotsider diff samples/RichLibrary/bin/Debug/net10.0/RichLibrary.dll samples/RichLibraryV2/bin/Debug/net10.0/RichLibrary.dll"
Sleep 500ms
Enter
Sleep 5s

# Cycle through diff subtabs: Summary, Types, Methods, References
Type "2"
Sleep 1500ms
Type "3"
Sleep 1500ms
Type "4"
Sleep 1500ms
Type "1"
Sleep 1500ms

# "Press f to cycle filters: All, Added, Removed, Changed."
Type "f"
Sleep 1500ms
Type "f"
Sleep 1500ms
Type "f"
Sleep 1500ms

Type "q"
Sleep 1500ms

# ── 6. Browse a NuGet package ─────────────────────────
# "dotsider package.nupkg"
# "Lists the package contents."
Type "dotsider samples/RichLibrary/bin/Debug/RichLibrary.2.5.1.nupkg"
Sleep 500ms
Enter
Sleep 5s

# "Press Enter on any DLL to open it in the full analyzer."
Enter
Sleep 3s

# Cycle through analyzer tabs on the opened DLL
Type "2"
Sleep 1500ms
Type "3"
Sleep 1500ms
Type "4"
Sleep 1500ms
Type "5"
Sleep 1500ms
Type "6"
Sleep 1500ms
Type "7"
Sleep 1500ms

# ── 7. Quit ───────────────────────────────────────────
# "Press q."
Type "q"
Sleep 1s
Type "q"
Sleep 1s
