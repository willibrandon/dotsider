# dotsider Documentation Screenshots (HiDPI)
# 2x pixel density capture while preserving the original terminal layout.
# Consistent: 2800x1600, fontSize 28, Catppuccin Mocha, RichLibrary.dll

Set Shell bash
Set FontSize 28
Set Width 2800
Set Height 1600
Set Padding 40
Set Theme "Catppuccin Mocha"
Set WindowBar Colorful
Set TypingSpeed 0ms
Set Framerate 25

Hide
Type "export PATH=/tmp/dotsider-bin:$PATH"
Enter
Sleep 500ms
Type "clear"
Enter
Sleep 500ms
Show

# Launch with RichLibrary.dll
Type "dotsider samples/RichLibrary/bin/Debug/net10.0/RichLibrary.dll"
Enter
Sleep 6s

# Tab 1: General
Screenshot docs/src/assets/screenshots/general.png
Sleep 2s

# Tab 2: PE/Metadata
Type "2"
Sleep 4s
Screenshot docs/src/assets/screenshots/pe-metadata.png
Sleep 2s

# Tab 3: IL Inspector — navigate to a method to show IL disassembly
Type "3"
Sleep 4s

# Navigate tree down to ProductCatalog (13 items from top — IlNavigationFixture added)
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms
Down
Sleep 200ms

# Expand ProductCatalog (Right expands tree nodes)
Right
Sleep 1s

# Down to first interesting method (skip AddProduct, go to Search)
Down
Sleep 200ms
Down
Sleep 200ms

# Select the method to show IL disassembly
Enter
Sleep 2s

Screenshot docs/src/assets/screenshots/il-inspector.png
Sleep 2s

# Tab 4: Strings
Type "4"
Sleep 4s
Screenshot docs/src/assets/screenshots/strings.png
Sleep 2s

# Tab 5: Hex Dump
Type "5"
Sleep 4s
Screenshot docs/src/assets/screenshots/hex-dump.png
Sleep 2s

# Tab 6: Dep Graph
Type "6"
Sleep 4s
Screenshot docs/src/assets/screenshots/dep-graph.png
Sleep 2s

# Tab 7: Size Map
Type "7"
Sleep 4s
Screenshot docs/src/assets/screenshots/size-map.png
Sleep 2s

# Quit RichLibrary session
Type "q"
Sleep 3s

# ── Dynamic tab with HelloWorld.dll (executable, can trace) ──
Type "dotsider samples/HelloWorld/bin/Debug/net10.0/HelloWorld.dll --tab 8"
Enter
Sleep 6s

# Launch trace with Enter
Enter
Sleep 8s

Screenshot docs/src/assets/screenshots/dynamic.png
Sleep 2s

# Quit HelloWorld session
Type "q"
Sleep 3s

# ── Diff mode ──
Type "dotsider diff samples/RichLibrary/bin/Debug/net10.0/RichLibrary.dll samples/RichLibraryV2/bin/Debug/net10.0/RichLibrary.dll"
Enter
Sleep 8s
Screenshot docs/src/assets/screenshots/diff-mode.png
Sleep 2s

Type "q"
Sleep 3s

# ── NuGet mode ──
Type "dotsider samples/RichLibrary/bin/Debug/RichLibrary.2.5.1.nupkg"
Enter
Sleep 8s
Screenshot docs/src/assets/screenshots/nuget-mode.png
Sleep 2s

Type "q"
Sleep 1s
