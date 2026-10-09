; O-view Windows installer (ADR-0010 D5/D6, slicing table row 7).
;
; Per-user install, no elevation. Writes the *same* HKCU Run value the in-app
; startup toggle (ADR-0009 D3, RegistryStartupRegistration) reads and writes,
; so there is exactly one authoritative "run at startup" setting, never two.
;
; Silent update mode: WindowsUpdateExecutor's ShellInstallerLauncher
; (src/O-view.Tray/Updates/IInstallerLauncher.cs) launches this installer with
; exactly "/SILENT /update=1". /SILENT already makes Inno skip every
; "skipifsilent" [Run] entry, which is what keeps the normal first-install
; "launch now" step from firing during an update (ADR-0010 D6). /update=1 is
; read below to gate the update path's own explorer.exe relaunch, which is a
; *different* [Run] entry — not a reuse of the launch-now one, since D6 wants
; the relaunch re-parented via explorer.exe, which bare "launch now" is not.
;
; The release workflow (ADR-0010 slicing table row 9, not built here) is the
; intended caller of ISCC; it supplies the real published app contents and the
; app's version, e.g.:
;   iscc /DSourceDir="publish\win-x64" /DMyAppVersion="1.4.0" installer\O-view.iss
; Both defines have fallbacks below so this script also compiles standalone.

#ifndef SourceDir
#define SourceDir "..\src\O-view.Tray\bin\Release\net10.0-windows\win-x64\publish"
#endif
#ifndef MyAppVersion
#define MyAppVersion "0.0.0"
#endif

#define MyAppName "O-view"
#define MyAppExeName "O-view.Tray.exe"
; Fixed once, never regenerated — Inno uses this GUID to recognise "this is the
; same app" across versions so an update upgrades in place instead of installing
; a second copy alongside the first.
#define MyAppId "{{6E6F0E7E-6E5A-4C4B-9E6A-5B6B6B1E9D3A}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=O-view
DefaultDirName={localappdata}\Programs\O-view
DisableProgramGroupPage=yes
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename=O-view-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Inno's Restart Manager integration (default CloseApplications=yes) is what
; D6 means by "finishes closing the old instance" before files are replaced.
; RestartApplications is turned off because the relaunch below — via
; explorer.exe, gated on /update=1 — is this installer's own D6 relaunch step,
; not Inno's generic RM-triggered restart; leaving both on would double-launch.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Tasks]
Name: "startup"; Description: "Start O-view automatically when I sign in"; Flags: unchecked

[Registry]
; Same subkey and value name as RegistryStartupRegistration.DefaultSubKeyPath /
; DefaultValueName (src/O-view.Tray/Platform/RegistryStartupRegistration.cs),
; and the same quoted-path format Enable() writes.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "O-view"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
; Normal first install's "launch now": skipped automatically under /SILENT, so
; a silent update never double-launches via this entry (ADR-0010 D6).
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall skipifsilent nowait

; D6's post-update relaunch: only when the app launched us with /update=1, run
; via explorer.exe so the new process is re-parented to the interactive user
; shell rather than inheriting the installer's own context, and returns
; immediately so there is no window flash.
Filename: "{win}\explorer.exe"; Parameters: """{app}\{#MyAppExeName}"""; Flags: nowait; Check: IsSilentUpdate

[Code]
function IsSilentUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:update|0}') = '1';
end;
