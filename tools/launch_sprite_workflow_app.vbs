Option Explicit

Dim shell
Dim fso
Dim scriptPath
Dim toolsDir
Dim powerShell
Dim command

Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

scriptPath = WScript.ScriptFullName
toolsDir = fso.GetParentFolderName(scriptPath)
powerShell = shell.ExpandEnvironmentStrings("%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe")

command = """" & powerShell & """" & " -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " & """" & fso.BuildPath(toolsDir, "launch_sprite_workflow_app.ps1") & """"

shell.Run command, 0, False
