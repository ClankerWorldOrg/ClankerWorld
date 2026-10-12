; Compiled from the same commit as the verified launcher and game packages.
#ifndef LauncherVersion
  #error LauncherVersion is required
#endif
#ifndef GameVersion
  #error GameVersion is required
#endif
#ifndef SourceRevision
  #error SourceRevision is required
#endif
#ifndef LauncherFiles
  #error LauncherFiles is required
#endif
#ifndef GameFiles
  #error GameFiles is required
#endif
#ifndef LauncherFileVersion
  #error LauncherFileVersion is required
#endif

[Setup]
AppId=ClankerWorld.Launcher
AppName=ClankerWorld Launcher
AppVersion={#LauncherVersion}
AppVerName=ClankerWorld Launcher {#LauncherVersion}
AppPublisher=ClankerWorld
VersionInfoVersion={#LauncherFileVersion}
VersionInfoDescription=ClankerWorld setup (game {#GameVersion})
VersionInfoProductVersion={#LauncherFileVersion}
VersionInfoProductTextVersion={#LauncherVersion}
VersionInfoTextVersion={#LauncherVersion}
SetupIconFile=..\src\ClankerWorld.GodotClient\icon.ico
DefaultDirName={localappdata}\Programs\ClankerWorld
DefaultGroupName=ClankerWorld
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
Compression=lzma2
SolidCompression=yes
OutputBaseFilename=ClankerWorld-Setup-{#GameVersion}-windows-x64
UninstallDisplayIcon={app}\ClankerWorldLauncher.exe
UninstallDisplayName=ClankerWorld Launcher
RestartApplications=no
SetupMutex=ClankerWorld.Launcher.Setup

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#LauncherFiles}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Stage on the destination volume, then publish the complete version folder.
; Game versions belong to the player, so the uninstaller never removes them.
Source: "{#GameFiles}\*"; DestDir: "{code:GetGameStage}"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Check: NeedBundledGame

[Icons]
Name: "{userprograms}\ClankerWorld\ClankerWorld"; Filename: "{app}\ClankerWorldLauncher.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\ClankerWorld"; Filename: "{app}\ClankerWorldLauncher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ClankerWorldLauncher.exe"; Description: "Start ClankerWorld"; Flags: nowait postinstall skipifsilent

[Messages]
ConfirmUninstall=Remove the ClankerWorld launcher and its shortcuts?%n%nYour installed game versions, worlds, keys and model-call count will be kept.

[Code]
var
  SeedGame: Boolean;
  GameStage: String;
  GameDestination: String;

function GetGameStage(Param: String): String;
begin
  Result := GameStage;
end;

function NeedBundledGame: Boolean;
begin
  Result := SeedGame;
end;

procedure InitializeWizard;
var
  ProgramsDirectory: String;
begin
  ProgramsDirectory := ExpandConstant('{localappdata}\Programs\ClankerWorld');
  if CompareText(RemoveBackslashUnlessRoot(WizardDirValue), ProgramsDirectory) <> 0 then
    RaiseException('Setup uses your Windows user programs folder. Run it without a custom installation folder.');
  GameDestination := ExpandConstant('{localappdata}\ClankerWorld\versions\{#GameVersion}');
  SeedGame := not DirExists(GameDestination);
  GameStage := ExpandConstant('{localappdata}\ClankerWorld\versions\.setup-') +
    ExtractFileName(RemoveBackslashUnlessRoot(ExpandConstant('{tmp}')));
  if SeedGame and (DirExists(GameStage) or FileExists(GameStage)) then begin
    SeedGame := False;
    RaiseException('The temporary setup folder is already in use. Close setup and try again.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and SeedGame then begin
    // Another launcher might have installed this version while setup ran.
    if DirExists(GameDestination) then begin
      DelTree(GameStage, True, True, True);
    end else if not RenameFile(GameStage, GameDestination) then
      RaiseException('The bundled game could not be installed. Your existing versions and worlds are safe; run setup again.');
  end;
end;

procedure DeinitializeSetup;
begin
  // Remove only this setup's unpublished staging folder after cancellation.
  if SeedGame and DirExists(GameStage) then
    DelTree(GameStage, True, True, True);
end;
