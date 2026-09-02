@echo off
rem Compile le lecteur du fichier de reglages (handle 0, lecture seule)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /warn:4 /main:Xt30BackupRead.Program /out:"%~dp0xt30-backup-read.exe" "%~dp0BackupRead.cs" "%~dp0..\..\Probe.cs"
if errorlevel 1 ( echo ECHEC DE COMPILATION & exit /b 1 )
echo OK : %~dp0xt30-backup-read.exe
