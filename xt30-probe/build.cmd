@echo off
rem Compile xt30-probe avec le compilateur C# integre a Windows (aucun SDK requis)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

rem 1) Version graphique finale (double-clic) : xt30-recipe-manager.exe
"%CSC%" /nologo /warn:4 /platform:anycpu /target:winexe /main:Xt30Probe.GuiProgram ^
  /win32icon:"%~dp0app.ico" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  /out:"%~dp0xt30-recipe-manager.exe" "%~dp0Probe.cs" "%~dp0Gui.cs" "%~dp0ReportViewer.cs" ^
  "%~dp0Models\*.cs" "%~dp0Camera\*.cs" "%~dp0Presentation\*.cs"
if errorlevel 1 (
  echo ECHEC DE COMPILATION GUI
  exit /b 1
)

rem 2) Version console : xt30-probe-cli.exe
"%CSC%" /nologo /warn:4 /platform:anycpu /main:Xt30Probe.Program ^
  /win32icon:"%~dp0app.ico" ^
  /out:"%~dp0xt30-probe-cli.exe" "%~dp0Probe.cs"
if errorlevel 1 (
  echo ECHEC DE COMPILATION CLI
  exit /b 1
)

echo OK : %~dp0xt30-recipe-manager.exe (interface graphique finale)
echo OK : %~dp0xt30-probe-cli.exe (console)
