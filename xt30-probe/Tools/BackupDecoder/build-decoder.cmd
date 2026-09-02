@echo off
rem Compile le decodeur de fichier de reglages (analyse de fichier, zero PTP)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /warn:4 /out:"%~dp0xt30-backup-decoder.exe" "%~dp0BackupDecoder.cs"
if errorlevel 1 ( echo ECHEC DE COMPILATION & exit /b 1 )
echo OK : %~dp0xt30-backup-decoder.exe
