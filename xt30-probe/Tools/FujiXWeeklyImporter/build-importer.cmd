@echo off
rem Compile l'importeur Fuji X Weekly (module independant du moteur camera)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /warn:4 /out:"%~dp0fxw-importer.exe" "%~dp0FxwImporter.cs"
if errorlevel 1 ( echo ECHEC DE COMPILATION & exit /b 1 )
echo OK : %~dp0fxw-importer.exe
