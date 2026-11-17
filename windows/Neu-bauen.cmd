@echo off
setlocal
pushd "%~dp0" || exit /b 1
set "DEBATE_CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%DEBATE_CSC%" set "DEBATE_CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%DEBATE_CSC%" (
  echo Der Windows-Compiler aus .NET Framework 4.8 wurde nicht gefunden.
  popd
  exit /b 1
)
"%DEBATE_CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /codepage:65001 /out:DEBATE-AI.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Management.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll src\*.cs
set "DEBATE_RESULT=%ERRORLEVEL%"
popd
exit /b %DEBATE_RESULT%
