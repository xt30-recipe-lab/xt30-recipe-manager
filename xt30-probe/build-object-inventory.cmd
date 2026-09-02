@echo off
rem Compile uniquement l'inventaire PTP phase 2. Ne reconstruit pas l'application GUI.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /warn:4 /platform:anycpu /main:Xt30Probe.ObjectInventoryProgram ^
  /win32icon:"%~dp0app.ico" ^
  /out:"%~dp0xt30-object-inventory.exe" "%~dp0Probe.cs" "%~dp0ObjectInventory.cs"
if errorlevel 1 (
  echo ECHEC DE COMPILATION INVENTAIRE
  exit /b 1
)

echo OK : %~dp0xt30-object-inventory.exe ^(lecture seule 0x1004/0x1005/0x1007/0x1008^)
