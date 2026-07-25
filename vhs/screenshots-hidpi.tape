# dotsider documentation screenshots
# HiDPI stills at a consistent 2800x1600 using current managed and native fixtures.

Require dotnet

Set Shell bash
Set FontSize 28
Set Width 2800
Set Height 1600
Set Padding 40
Set Theme "Catppuccin Mocha"
Set WindowBar Colorful
Set TypingSpeed 0ms
Set Framerate 1
Set WaitTimeout 90s

Hide
Source vhs/setup.tape
Show

# Managed assembly overview and PE metadata.
Type "dotsider samples/RichLibrary/bin/Debug/net10.0/RichLibrary.dll"
Enter
Wait+Screen /Assembly References/
Screenshot docs/src/assets/screenshots/general.png
Sleep 4s

Type "2"
Wait+Screen /PE Headers/
Screenshot docs/src/assets/screenshots/pe-metadata.png
Sleep 4s

Type "q"
Wait

# Native AOT with its pre-ILC companion, mstat, DGML, and dSYM sidecars.
Type "dotsider $AOT_V1"
Enter
Wait+Screen /Native AOT Sidecars Detected/
Enter
Wait+Screen /Assembly References \(pre-ILC\)/

# The side-by-side IL/native screenshot uses a wider terminal grid and is
# generated separately by screenshots-il-hidpi.tape.

# Frozen string literals recovered from the Native AOT image.
Type "4"
Wait+Screen /User Strings/
Right 4
Wait+Screen /Frozen \(AOT\)/
Type "/"
Sleep 1s
Type "dotsider"
Sleep 2s
Enter
Sleep 3s
Screenshot docs/src/assets/screenshots/strings.png
Sleep 4s
Escape
Sleep 2s

# Mach-O/ELF/PE bytes, native dependency graph, and ILC size map.
Type "5"
Wait+Screen /Data Interpretation/
Screenshot docs/src/assets/screenshots/hex-dump.png
Sleep 4s

Type "6"
Wait+Screen /Hover over a node for details/
Screenshot docs/src/assets/screenshots/dep-graph.png
Sleep 4s

Type "7"
Wait+Screen /Total:/
Screenshot docs/src/assets/screenshots/size-map.png
Sleep 4s

Type "q"
Wait

# EventPipe tracing of the async pipeline sample.
Type "dotsider samples/ComplexApp/bin/Debug/net10.0/ComplexApp.dll --tab 8"
Enter
Wait+Screen /Press Enter to launch with EventPipe tracing/
Sleep 2s
Enter
Wait+Screen /Events:/
Sleep 3s
Screenshot docs/src/assets/screenshots/dynamic.png
Sleep 4s

Type "q"
Wait

# Managed API diff.
Type "dotsider diff samples/RichLibrary/bin/Debug/net10.0/RichLibrary.dll samples/RichLibraryV2/bin/Debug/net10.0/RichLibrary.dll"
Enter
Wait+Screen /Change Summary/
Screenshot docs/src/assets/screenshots/diff-mode.png
Sleep 4s

Type "q"
Wait

# NuGet package browser.
Type "dotsider samples/RichLibrary/bin/Debug/RichLibrary.2.5.1.nupkg"
Enter
Wait+Screen /Package Info/
Screenshot docs/src/assets/screenshots/nuget-mode.png
Sleep 4s

Type "q"
Wait

# Native AOT size regression delta treemap.
Type "dotsider diff artifacts/vhs/before.mstat artifacts/vhs/after.mstat"
Enter
Wait+Screen /changed entries/
Type "2"
Sleep 4s
Screenshot docs/src/assets/screenshots/size-regression.png
Sleep 4s

Type "q"
Sleep 2s
