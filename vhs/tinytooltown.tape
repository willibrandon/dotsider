# dotsider TinyToolTown thumbnail
# A compact General view using the representative managed fixture.

Require dotnet

Set Shell bash
Set FontSize 14
Set Width 1200
Set Height 600
Set Padding 20
Set Theme "Catppuccin Mocha"
Set WindowBar Colorful
Set TypingSpeed 0ms
Set Framerate 1
Set WaitTimeout 90s

Hide
Source vhs/setup.tape
Show

Type "dotsider samples/RichLibrary/bin/Debug/net10.0/RichLibrary.dll"
Enter
Wait+Screen /Assembly References/
Sleep 2s
Screenshot assets/tinytooltown.png
Sleep 4s

Type "q"
Sleep 2s
