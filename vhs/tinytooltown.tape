# dotsider TinyToolTown thumbnail
# 1200x600 at 1x density, exact 16:9 for TinyToolTown card layout
# Shows the General tab — the first thing you see after launching dotsider

Set Shell bash
Set FontSize 14
Set Width 1200
Set Height 600
Set Padding 20
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

# Launch with HelloWorld.dll
Type "dotsider samples/HelloWorld/bin/Debug/net10.0/HelloWorld.dll"
Enter
Sleep 6s

Screenshot assets/tinytooltown.png
Sleep 1s

Type "q"
Sleep 1s
