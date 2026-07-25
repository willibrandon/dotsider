# dotsider quick-start demo

Output docs/public/demo.gif

Require dotnet

Set Shell bash
Set FontSize 14
Set Width 1792
Set Height 1008
Set Padding 20
Set Theme "Catppuccin Mocha"
Set WindowBar Colorful
Set TypingSpeed 15ms
Set Framerate 20
Set WaitTimeout 90s

Hide
Source vhs/setup.tape
Show

# Correlate a Native AOT binary with its pre-ILC assembly.
Type "dotsider artifacts/a/publish/n/h/NativeAotConsole"
Sleep 2s
Enter
Wait+Screen /Native AOT Sidecars Detected/
Sleep 2s
Enter
Wait+Screen /Assembly References \(pre-ILC\)/
Sleep 3s

# Walk every analysis tab in order.
Hide
Type "2"
Wait+Screen /PE Headers/
Right 12
Wait+Screen /System\.Object/
Show
Sleep 4s

Type "3"
Wait+Screen /Select a method to view IL disassembly/
Down 2
Right
Down 2
Enter
Wait+Screen /native: .*get_Name/
Sleep 4s

Type "4"
Wait+Screen /User Strings/
Sleep 2s
Type "/"
Sleep 500ms
Type@200ms "me"
Sleep 2s
Enter
Sleep 1s
Type "n"
Sleep 1s
Type "n"
Sleep 1s
Type "N"
Sleep 2s
Escape
Sleep 1s

Right 4
Wait+Screen /Frozen \(AOT\)/
Sleep 3s

Type "5"
Wait+Screen /Data Interpretation/
Sleep 3s

Type "6"
Wait+Screen /Hover over a node for details/
Sleep 3s

Type "7"
Wait+Screen /Total:/
Sleep 3s

Type "q"
Wait

# Compare two Native AOT size reports.
Type "dotsider diff artifacts/vhs/before.mstat artifacts/vhs/after.mstat"
Sleep 2s
Enter
Wait+Screen /changed entries/
Sleep 3s

Type "2"
Sleep 4s

Type "q"
Wait

# Trace a real async application through EventPipe.
Type "dotsider samples/ComplexApp/bin/Debug/net10.0/ComplexApp.dll --tab 8"
Sleep 2s
Enter
Wait+Screen /Press Enter to launch with EventPipe tracing/
Sleep 2s
Enter
Wait+Screen /Events:/
Sleep 4s

Type "q"
Wait

# Browse a NuGet package and open its managed assembly.
Type "dotsider samples/RichLibrary/bin/Debug/RichLibrary.2.5.1.nupkg"
Sleep 2s
Enter
Wait+Screen /Package Info/
Sleep 3s
Enter
Wait+Screen /Assembly References/
Sleep 3s

Type "q"
Sleep 1s
Type "q"
Sleep 2s
