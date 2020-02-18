# mapbasic README
Try out this visual studio code language extension we have created for the mapbasic language.
Currently it supports syntax highlighting and some snippets along with a build task for compiling and linking.

To use. Open up the folder where your mapbasic application lives.
For example c:\program files\mapbasic\samples\animator

You should be able to see syntax highlighting and some snippets.

To use the build task, you currently have to copy the tasks.json file from the language install folder into the .vscode folder in your application folder.
For example copy %USERPROFILE%\\.vscode\extensions\pitneybowesmapinfo.mapbasic-0.0.8\tasks.json to  c:\program files\mapbasic\samples\animator\.vscode\tasks.json

Then you can use the build task ( Ctrl+Shift+B ) to build your project and see any errors in the problems window (Ctrl + Shift +P).
The build tasks calls mapbasic with either the mapbasic files in your folder, or if you have a project file (.mbp) it calls mapbasic with the modules listed in there and also passes the link flag to mapbasic.
Note that if there is a .mb file with the same name as the module it will try to compile it, but if there is no mb file it uses the .mbo.
Also note that mapbasic does not remove the previous .mbo when a compile has errors.
So it can build the whole project at once.


