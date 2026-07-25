# dotsider IL/native documentation screenshot
# A wider terminal grid at the standard 2800x1600 output size keeps both code panes visible.

Require dotnet

Set Shell bash
Set FontSize 22
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

Type "dotsider $VHS_AOT_PUBLISH/NativeAotConsole"
Enter
Wait+Screen /Native AOT Sidecars Detected/
Enter
Wait+Screen /Assembly References \(pre-ILC\)/

Type "3"
Wait+Screen /Select a method to view IL disassembly/
Down 2
Right
Down 2
Enter
Wait+Screen /native: .*get_Name/
Sleep 2s
Screenshot docs/src/assets/screenshots/il-inspector.png
Sleep 4s
