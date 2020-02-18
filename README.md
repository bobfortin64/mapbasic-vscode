# mapbasic-vscode
A visual studio code language extension for MapInfo MapBasic

To build the helper exe build MapBasicHelper\BuildMapBasicHelper.csproj
Then copy the results from bin\Release into LanguageExtension\MapBasic
mapbasic-vscode\MapBasicHelper\bin\Release\netcoreapp3.0\MapBasicHelper.dll
mapbasic-vscode\MapBasicHelper\bin\Release\netcoreapp3.0\MapBasicHelper.exe
mapbasic-vscode\MapBasicHelper\bin\Release\netcoreapp3.0\MapBasicHelper.runtimeconfig.json

To build the vscode language extension: C:\work\GitHub\mapbasic-vscode\LanguageExtension\mapbasic>vsce package
See https://code.visualstudio.com/api/working-with-extensions/publishing-extension for info on how to build an extension

