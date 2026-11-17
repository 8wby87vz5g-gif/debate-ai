@echo off
setlocal
pushd "%~dp0" || exit /b 1
set "DEBATE_CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%DEBATE_CSC%" set "DEBATE_CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%DEBATE_CSC%" /nologo /target:exe /warnaserror+ /codepage:65001 /out:LicenseTool.exe /main:DebateAI.LicenseTool /r:System.Web.Extensions.dll /r:System.Security.dll src\License.cs src\LocalData.cs ..\tools\LicenseTool.cs
set "DEBATE_RESULT=%ERRORLEVEL%"
popd
exit /b %DEBATE_RESULT%
