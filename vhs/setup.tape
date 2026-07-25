# Shared, hidden setup for all dotsider VHS captures.
# The parent tape supplies Hide/Show so build output never enters the recording.

Type "mkdir -p artifacts/vhs"
Enter
Wait

Type "dotnet publish src/Dotsider/Dotsider.csproj -c Release -p:PathMap=$PWD=/src/dotsider -o artifacts/vhs/dotsider >/dev/null"
Enter
Wait

Type "dotnet build samples/RichLibrary/RichLibrary.csproj -c Debug -p:PathMap=$PWD=/src/dotsider >/dev/null"
Enter
Wait

Type "dotnet build samples/RichLibraryV2/RichLibraryV2.csproj -c Debug -p:PathMap=$PWD=/src/dotsider >/dev/null"
Enter
Wait

Type "dotnet build samples/ComplexApp/ComplexApp.csproj -c Debug -p:PathMap=$PWD=/src/dotsider >/dev/null"
Enter
Wait

Type "dotnet pack samples/RichLibrary/RichLibrary.csproj -c Debug --no-build -o samples/RichLibrary/bin/Debug >/dev/null"
Enter
Wait

Type "RID=$(dotnet --info | awk '/RID:/{print $2; exit}')"
Enter
Wait

Type "dotnet publish samples/NativeAotConsole/NativeAotConsole.csproj -c Release -r $RID -p:PathMap=$PWD=/src/dotsider >/dev/null"
Enter
Wait

Type "dotnet publish samples/NativeAotConsoleV2/NativeAotConsoleV2.csproj -c Release -r $RID -p:PathMap=$PWD=/src/dotsider >/dev/null"
Enter
Wait

Type "AOT_V1_BASE=samples/NativeAotConsole/bin/Release/net10.0/$RID/publish/NativeAotConsole"
Enter
Wait

Type "AOT_V2_BASE=samples/NativeAotConsoleV2/bin/Release/net10.0/$RID/publish/NativeAotConsole"
Enter
Wait

Type "AOT_V1=$AOT_V1_BASE; test -f $AOT_V1 || AOT_V1=$AOT_V1_BASE.exe"
Enter
Wait

Type "AOT_V1_BUILD=samples/NativeAotConsole/bin/Release/net10.0/$RID"
Enter
Wait

Type "VHS_AOT_PUBLISH=artifacts/a/publish/n/h"
Enter
Wait

Type "VHS_AOT_OBJ=artifacts/a/obj/n/h"
Enter
Wait

Type "VHS_AOT_NATIVE=$VHS_AOT_OBJ/native"
Enter
Wait

Type "rm -rf artifacts/a && mkdir -p $VHS_AOT_PUBLISH $VHS_AOT_NATIVE"
Enter
Wait

Type "cp $AOT_V1 $VHS_AOT_PUBLISH/NativeAotConsole"
Enter
Wait

Type "cp $AOT_V1_BUILD/NativeAotConsole.dll $VHS_AOT_OBJ/NativeAotConsole.dll"
Enter
Wait

Type "cp $AOT_V1_BUILD/NativeAotConsole.pdb $VHS_AOT_OBJ/NativeAotConsole.pdb"
Enter
Wait

Type "cp $AOT_V1_BASE.mstat $VHS_AOT_NATIVE/NativeAotConsole.mstat"
Enter
Wait

Type "cp $AOT_V1_BASE.codegen.dgml.xml $VHS_AOT_NATIVE/NativeAotConsole.codegen.dgml.xml"
Enter
Wait

Type "cp $AOT_V1_BASE.scan.dgml.xml $VHS_AOT_NATIVE/NativeAotConsole.scan.dgml.xml"
Enter
Wait

Type "test ! -f $AOT_V1_BASE.pdb || cp $AOT_V1_BASE.pdb $VHS_AOT_PUBLISH/NativeAotConsole.pdb"
Enter
Wait

Type "test ! -f $AOT_V1_BASE.dbg || cp $AOT_V1_BASE.dbg $VHS_AOT_PUBLISH/NativeAotConsole.dbg"
Enter
Wait

Type "test ! -d $AOT_V1_BASE.dSYM || cp -R $AOT_V1_BASE.dSYM $VHS_AOT_PUBLISH/NativeAotConsole.dSYM"
Enter
Wait

Type "cp $AOT_V1_BASE.mstat artifacts/vhs/before.mstat"
Enter
Wait

Type "cp $AOT_V2_BASE.mstat artifacts/vhs/after.mstat"
Enter
Wait

Type "export PATH=$PWD/artifacts/vhs/dotsider:$PATH"
Enter
Wait

Type "clear"
Enter
Wait
