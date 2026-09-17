; Inno Setup 6 Script for FrySharp (C# Code Studio) by Code Fry Dev
#ifndef MyAppVersion
#define MyAppVersion "1.0.1"
#endif

#ifndef MyAppVersionNumeric
#define MyAppVersionNumeric "1.0.1.0"
#endif

#ifndef MyPublishDir
#define MyPublishDir "..\..\publish\win-x64"
#endif

#define MyAppName "FrySharp"
#define MyAppPublisher "Code Fry Dev"
#define MyAppCopyright "Copyright (C) 2026 Code Fry Dev"
#define MyAppURL "https://codefrydev.in"
#define MyAppSupportURL "mailto:codefrydev@gmail.com"
#define MyAppExeName "FrySharp.exe"

[Setup]
; Basic Application Info
AppId={{C4781A92-6F09-4D8B-9AE2-C1A2B56789DE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppSupportURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}

; Add/Remove Programs
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName},0

; Destination Directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output Configuration
OutputDir=.
OutputBaseFilename=FrySharp-Setup-{#MyAppVersion}
SetupIconFile=..\..\Runner\Assets\app-logo.ico

; Wizard Branding
WizardStyle=modern
WizardImageFile=branding\wizard-large-*.png
WizardSmallImageFile=branding\wizard-small-*.png

; setup.exe Version Info
VersionInfoVersion={#MyAppVersionNumeric}
VersionInfoProductVersion={#MyAppVersionNumeric}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductTextVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoOriginalFileName=FrySharp-Setup.exe

; Compression & Behaviour
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
SetupMutex=FrySharpSetupMutex
CloseApplications=yes
RestartApplications=no

; File Associations
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
SetupAppTitle={#MyAppName} Setup
SetupWindowTitle={#MyAppName} Setup
UninstallAppTitle={#MyAppName} Uninstall
UninstallAppFullTitle={#MyAppName} Uninstall

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc_cs"; Description: "Associate with C# Source Files (.cs)"; GroupDescription: "File Associations:"; Flags: unchecked
Name: "fileassoc_csx"; Description: "Associate with C# Script Files (.csx)"; GroupDescription: "File Associations:"
Name: "fileassoc_frycs"; Description: "Associate with FrySharp Workspace Files (.frycs)"; GroupDescription: "File Associations:"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Open-with list
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey

; .csx Association
Root: HKA; Subkey: "Software\Classes\.csx"; ValueType: string; ValueName: ""; ValueData: "FrySharp.Script"; Flags: uninsdeletevalue; Tasks: fileassoc_csx
Root: HKA; Subkey: "Software\Classes\FrySharp.Script"; ValueType: string; ValueName: ""; ValueData: "C# Script File"; Flags: uninsdeletekey; Tasks: fileassoc_csx
Root: HKA; Subkey: "Software\Classes\FrySharp.Script\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: fileassoc_csx
Root: HKA; Subkey: "Software\Classes\FrySharp.Script\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: fileassoc_csx

; .frycs Association
Root: HKA; Subkey: "Software\Classes\.frycs"; ValueType: string; ValueName: ""; ValueData: "FrySharp.Workspace"; Flags: uninsdeletevalue; Tasks: fileassoc_frycs
Root: HKA; Subkey: "Software\Classes\FrySharp.Workspace"; ValueType: string; ValueName: ""; ValueData: "FrySharp Workspace File"; Flags: uninsdeletekey; Tasks: fileassoc_frycs
Root: HKA; Subkey: "Software\Classes\FrySharp.Workspace\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: fileassoc_frycs
Root: HKA; Subkey: "Software\Classes\FrySharp.Workspace\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: fileassoc_frycs

; .cs Association (optional)
Root: HKA; Subkey: "Software\Classes\.cs\OpenWithProgids"; ValueType: string; ValueName: "FrySharp.Source"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc_cs
Root: HKA; Subkey: "Software\Classes\FrySharp.Source"; ValueType: string; ValueName: ""; ValueData: "C# Source File"; Flags: uninsdeletekey; Tasks: fileassoc_cs
Root: HKA; Subkey: "Software\Classes\FrySharp.Source\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: fileassoc_cs
Root: HKA; Subkey: "Software\Classes\FrySharp.Source\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: fileassoc_cs

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
