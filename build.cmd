@echo off
rem Builds LidFlip.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No Visual Studio or SDK needed.
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find csc.exe from the .NET Framework.
  exit /b 1
)
pushd "%~dp0"
"%CSC%" /nologo /target:winexe /optimize+ /out:LidFlip.exe /win32icon:assets\LidFlip.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs
set ERR=%ERRORLEVEL%
popd
if not %ERR%==0 exit /b %ERR%
echo Built LidFlip.exe
