; -- Example1.iss --
; Demonstrates copying 3 files and creating an icon.

; SEE THE DOCUMENTATION FOR DETAILS ON CREATING .ISS SCRIPT FILES!

; -- ISPPExample1.iss --
;
; This script shows various basic things you can achieve using Inno Setup Preprocessor (ISPP).
; To enable commented #define's, either remove the ';' or use ISCC with the /D switch.

#pragma option -v+
#pragma verboselevel 9

#define Debug

;#define AppEnterprise

#define AppName "Pitara"

#define AppVersion GetFileVersion(AddBackslash(SourcePath) + "..\Pitara\PitaraApp\bin\Release\net48\Pitara.exe")

#define SetupBaseName   "PitaraSetup"


[Setup]
AppVersion={#AppVersion}
AppPublisher=http://getpitara.com
AppPublisherURL=http://getpitara.com
AppSupportURL=http://getpitara.com
AppUpdatesURL=http://getpitara.com

UsePreviousAppDir= false
OutputBaseFilename={#SetupBaseName}
AppName={#AppName}
DefaultDirName={commonpf}\{#AppName}
DisableDirPage = no
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppName}.exe
LicenseFile={#file AddBackslash(SourcePath) + "License.txt"}
VersionInfoVersion={#AppVersion}

Compression=lzma2
SolidCompression=yes
OutputDir="..\Build"
WizardImageFile="tagEZBigImage.bmp"
WizardSmallImageFile="pitara.bmp"

[Files]
Source: "..\PackageContents\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\{#AppName}.exe"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppName}.exe"
Name: "{app}\{#AppName}"; Filename: "{app}\{#AppName}.exe"

[Run]
Filename: {app}\{cm:AppName}.exe; Description: {cm:LaunchProgram,{cm:AppName}}; Flags: nowait postinstall skipifsilent

[CustomMessages]
AppName={#AppName}
LaunchProgram=Run {#AppName} after finishing installation
