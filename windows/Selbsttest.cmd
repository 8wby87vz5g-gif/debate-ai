@echo off
setlocal
pushd "%~dp0" || exit /b 1
set "DEBATE_CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%DEBATE_CSC%" set "DEBATE_CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "DEBATE_TEST=%TEMP%\DebateAI-test-%RANDOM%-%RANDOM%.exe"
"%DEBATE_CSC%" /nologo /main:DebateAI.Tests /target:exe /warnaserror+ /codepage:65001 /out:"%DEBATE_TEST%" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll src\Audio.cs src\LocalData.cs src\MainForm.cs src\Realtime.cs src\Turns.cs tests\Tests.cs
set "DEBATE_RESULT=%ERRORLEVEL%"
if not "%DEBATE_RESULT%"=="0" goto end
"%DEBATE_TEST%"
set "DEBATE_RESULT=%ERRORLEVEL%"
:end
if exist "%DEBATE_TEST%" del "%DEBATE_TEST%"
popd
exit /b %DEBATE_RESULT%
