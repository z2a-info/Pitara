@echo off
REM hi and hello - test
REM test24-1
set PITARA_HOME=C:\Users\NarenDev\DATA\WorkSpace\temp2\Pitara\src
rd /s /q %PITARA_HOME%\Pitara\PitaraApp\bin
rd /s /q %PITARA_HOME%\PackageContents
mkdir %PITARA_HOME%\PackageContents


echo Nuget Restore
dotnet restore %PITARA_HOME%\src\Pitara\PitaraApp

echo Building Common ....
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"  ..\Pitara\CommonProject\CommonProject.sln /t:Rebuild  /p:Configuration=Release /p:DefineConstants="NO_EXPIRY"
if ERRORLEVEL 1 goto :showerror

echo Building App ....
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"  ..\Pitara\PitaraApp.sln /t:Rebuild  /p:Configuration=Release /p:DefineConstants="NO_EXPIRY"
if ERRORLEVEL 1 goto :showerror


REM echo Signing the app..
REM "C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe" sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /f "..\Cert\pitara_code_signing.pfx" /p SankyNew123 %PITARA_HOME%\Pitara\PitaraApp\bin\Release\net48\Pitara.exe
REM if ERRORLEVEL 1 goto :showerror

echo Copying to PackageContents.
xcopy /DIEFY %PITARA_HOME%\Pitara\PitaraApp\bin\Release\net48\ ..\PackageContents
if ERRORLEVEL 1 goto :showerror

del %PITARA_HOME%\PackageContents\*.pdb
if ERRORLEVEL 1 goto :showerror

echo Building setup ...
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Pitara.iss 
if ERRORLEVEL 1 goto :showerror

echo Copying Changelogs.
copy /y "%PITARA_HOME%\Pitara\PitaraApp\Changelog.md" ..\Build
if ERRORLEVEL 1 goto :showerror

echo Creating the zip
powershell.exe Compress-Archive -Force  -Path ..\PackageContents\ -DestinationPath ..\build\Pitara.zip
if ERRORLEVEL 1 goto :showerror

echo Signing the Setup
REM powershell.exe Set-AuthenticodeSignature ..\build\PitaraSetup.exe -Certificate (Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert)[0] 
REM "C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe" sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /f "..\Cert\pitara_code_signing.pfx" /p SankyNew123 ..\build\PitaraSetup.exe
REM if ERRORLEVEL 1 goto :showerror

REM echo Signing the Zip
REM "C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe" sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /f "..\Cert\pitara_selfsigning_cert.pfx" ..\build\Pitara.zip
REM if ERRORLEVEL 1 goto :showerror

timeout /t 3

echo Creating the zip
powershell.exe Compress-Archive -Force  -Path ..\build\PitaraSetup.exe -DestinationPath ..\build\PitaraSetup.zip
if ERRORLEVEL 1 goto :showerror

echo Creating SHA256 hash
certutil -hashfile ..\build\PitaraSetup.zip SHA256 > sha256Setup.txt

echo Creating SHA256 hash
certutil -hashfile ..\build\Pitara.zip SHA256 > sha256Zip.txt

"..\Pitara\PitaraApp\bin\Release\net48\Pitara.exe" Version
if ERRORLEVEL 1 goto :showerror

"..\Pitara\PitaraApp\bin\Release\net48\Pitara.exe" hash %PITARA_HOME%\Pitara\PitaraApp\DownloadAndHashTemplate.txt
if ERRORLEVEL 1 goto :showerror

"..\Pitara\PitaraApp\bin\Release\net48\Pitara.exe" hash %PITARA_HOME%\Pitara\PitaraApp\DownloadAndHashTemplate.txt
if ERRORLEVEL 1 goto :showerror


copy /y "%PITARA_HOME%\Pitara\PitaraApp\Changelog.md" ..\setup
if ERRORLEVEL 1 goto :showerror

"..\Pitara\PitaraApp\bin\Release\net48\Pitara.exe" downloadpage %PITARA_HOME%\Pitara\PitaraApp\Download-Pitara.template.md
if ERRORLEVEL 1 goto :showerror

echo Copying PitaraVersion.txt
copy /y PitaraVersion.txt ..\build\
if ERRORLEVEL 1 goto :showerror

echo Copying Download-Pitara.md
copy /y default.md ..\build\
if ERRORLEVEL 1 goto :showerror

echo Copying DownloadAndHash.txt
copy /y DownloadAndHash.txt ..\build\
if ERRORLEVEL 1 goto :showerror


type PitaraVersion.txt

goto :done

:showerror
echo Error occurred

:done
echo Done.. Setup

