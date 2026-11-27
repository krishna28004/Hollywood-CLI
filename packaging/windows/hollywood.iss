#ifndef AppVersion
#define AppVersion "0.2.0"
#endif

#ifndef PayloadRoot
#define PayloadRoot "..\..\artifacts\installer\publish"
#endif

[Setup]
AppId={{C6B24AA2-1A28-4F6C-9C4E-4C2DB93D18D5}
AppName=Hollywood CLI
AppVersion={#AppVersion}
AppVerName=Hollywood CLI {#AppVersion}
AppPublisher=Hollywood CLI contributors
DefaultDirName={localappdata}\Programs\Hollywood
DefaultGroupName=Hollywood CLI
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible or arm64
ArchitecturesInstallIn64BitMode=x64compatible or arm64
UsePreviousAppDir=yes
Uninstallable=yes
UninstallDisplayName=Hollywood CLI
SetupLogging=yes
ChangesEnvironment=no
OutputDir=..\..\artifacts\installer\releases\windows
OutputBaseFilename=Hollywood-Setup
#ifdef TestFixture
Compression=none
#else
Compression=lzma2
SolidCompression=yes
#endif
WizardStyle=modern
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PayloadRoot}\win-x64\hollywood.exe"; DestDir: "{app}\versions\{#AppVersion}\win-x64"; Flags: ignoreversion; Check: not IsArm64
Source: "{#PayloadRoot}\win-arm64\hollywood.exe"; DestDir: "{app}\versions\{#AppVersion}\win-arm64"; Flags: ignoreversion; Check: IsArm64
Source: "{#PayloadRoot}\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\DOTNET-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\LICENSE-INFORMATION.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\INNO-SETUP-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
#ifdef IncludeProjectLicense
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
#endif
#ifdef IncludeProjectNotice
Source: "..\..\NOTICE"; DestDir: "{app}"; Flags: ignoreversion
#endif

[Run]
Filename: "{app}\versions\{#AppVersion}\{code:RuntimeFolder}\hollywood.exe"; Description: "Launch Hollywood now"; Flags: postinstall nowait skipifsilent; Check: CanLaunchHollywood

[Code]
const
  AppUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C6B24AA2-1A28-4F6C-9C4E-4C2DB93D18D5}_is1';
  AppInstallStateKey = 'Software\Hollywood CLI\Installer';
  PathValueName = 'Path';
  PathOwnedValueName = 'HollywoodPathAdded';
  CommandValueName = 'HollywoodCommand';
  RuntimeValueName = 'HollywoodRuntime';
  MoveFileReplaceExisting = 1;
  MoveFileWriteThrough = 8;
  HwndBroadcast = $FFFF;
  WmSettingChange = $001A;
  SmtoAbortIfHung = $0002;

var
  SelectedCommand: String;
  SelectedRuntime: String;
  ExistingInstallDir: String;
  InstallFailed: Boolean;
  InstallFailureMessage: String;

function MoveFileExW(ExistingFileName, NewFileName: String; Flags: Cardinal): Boolean;
  external 'MoveFileExW@kernel32.dll stdcall';
function SendMessageTimeoutW(Wnd: HWND; Msg: Cardinal; WParam: Longint; LParam: String;
  Flags, Timeout: Cardinal; var ResultValue: Longint): Longint;
  external 'SendMessageTimeoutW@user32.dll stdcall';

function NormalizeDir(Value: String): String;
begin
  Result := Lowercase(Trim(Value));
  while (Length(Result) > 3) and (Result[Length(Result)] = '\') do
    Delete(Result, Length(Result), 1);
end;

function UnquotePath(Value: String): String;
begin
  Result := Trim(Value);
  if (Length(Result) >= 2) and (Result[1] = '"') and
    (Result[Length(Result)] = '"') then
    Result := Copy(Result, 2, Length(Result) - 2);
end;

function ReadExistingInstallDir: String;
begin
  Result := '';
  RegQueryStringValue(HKCU, AppUninstallKey, 'Inno Setup: App Path', Result);
end;

function IsExistingInstallPath(Value: String): Boolean;
begin
  Result := (ExistingInstallDir <> '') and
    (NormalizeDir(Value) = NormalizeDir(ExistingInstallDir));
end;

function PathEntryContainsCommand(Entry, Command: String): Boolean;
var
  BaseName: String;
begin
  Result := False;
  Entry := UnquotePath(Entry);
  if Entry = '' then
    Exit;
  if IsExistingInstallPath(Entry) then
    Exit;
  BaseName := AddBackslash(Entry) + Command;
  Result := FileExists(BaseName) or FileExists(BaseName + '.exe') or FileExists(BaseName + '.cmd') or
    FileExists(BaseName + '.bat') or FileExists(BaseName + '.com');
end;

function FindCommandOwnerInPath(PathValue, Command: String): String;
var
  Separator: Integer;
  Entry: String;
begin
  Result := '';
  while PathValue <> '' do begin
    Separator := Pos(';', PathValue);
    if Separator = 0 then begin
      Entry := PathValue;
      PathValue := '';
    end else begin
      Entry := Copy(PathValue, 1, Separator - 1);
      Delete(PathValue, 1, Separator);
    end;
    if PathEntryContainsCommand(Entry, Command) then begin
      Result := RemoveQuotes(Trim(Entry));
      Exit;
    end;
  end;
end;

function FindCommandOwner(Command: String): String;
var
  PathValue: String;
begin
  Result := FindCommandOwnerInPath(GetEnv('PATH'), Command);
  if Result <> '' then
    Exit;
  if RegQueryStringValue(HKCU, 'Environment', PathValueName, PathValue) then begin
    Result := FindCommandOwnerInPath(PathValue, Command);
    if Result <> '' then
      Exit;
  end;
  if RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    PathValueName, PathValue) then
    Result := FindCommandOwnerInPath(PathValue, Command);
end;

function RuntimeFolder(Param: String): String;
begin
  Result := SelectedRuntime;
end;

function CanLaunchHollywood: Boolean;
begin
  Result := not InstallFailed;
end;

function GetCustomSetupExitCode: Integer;
begin
  if InstallFailed then
    Result := 4
  else
    Result := 0;
end;

procedure SetInstallFailure(Message: String);
begin
  InstallFailed := True;
  InstallFailureMessage := Message;
  Log('Hollywood CLI installation failed: ' + Message);
end;

function InitializeSetup: Boolean;
var
  HollywoodOwner: String;
  CliOwner: String;
  StoredCommand: String;
begin
  Result := False;
  InstallFailed := False;
  InstallFailureMessage := '';
  ExistingInstallDir := ReadExistingInstallDir;
  if IsArm64 then
    SelectedRuntime := 'win-arm64'
  else if IsX64Compatible then
    SelectedRuntime := 'win-x64'
  else begin
    MsgBox('Hollywood CLI requires 64-bit Windows (x64 or ARM64).', mbError, MB_OK);
    Exit;
  end;

  if (ExistingInstallDir <> '') and RegQueryStringValue(HKCU, AppInstallStateKey,
    CommandValueName, StoredCommand) then begin
    HollywoodOwner := FindCommandOwner('hollywood');
    if (StoredCommand = 'hollywood') and (HollywoodOwner = '') then begin
      SelectedCommand := 'hollywood';
      Result := True;
      Exit;
    end;
    if (StoredCommand = 'hollywood-cli') and (HollywoodOwner = '') and
      FileExists(AddBackslash(ExistingInstallDir) + 'hollywood.cmd') then begin
      { Recover the primary name if a previous update fell back unnecessarily. }
      SelectedCommand := 'hollywood';
      Result := True;
      Exit;
    end;
    if (StoredCommand = 'hollywood-cli') and (FindCommandOwner('hollywood-cli') = '') then begin
      SelectedCommand := 'hollywood-cli';
      Result := True;
      Exit;
    end;
  end;

  HollywoodOwner := FindCommandOwner('hollywood');
  if HollywoodOwner = '' then
    SelectedCommand := 'hollywood'
  else begin
    CliOwner := FindCommandOwner('hollywood-cli');
    if CliOwner <> '' then begin
      MsgBox('The command "hollywood" is already provided by:' + #13#10 +
        HollywoodOwner + Chr(13) + Chr(10) + Chr(13) + Chr(10) + 'The safe alternative "hollywood-cli" is also in use at:' +
        Chr(13) + Chr(10) + CliOwner + Chr(13) + Chr(10) + Chr(13) + Chr(10) +
        'Remove or rename one of those commands, then run setup again. No existing command was changed.',
        mbError, MB_OK);
      Exit;
    end;
    SelectedCommand := 'hollywood-cli';
  end;

  Result := True;
end;

procedure InitializeWizard;
begin
  if SelectedCommand = 'hollywood-cli' then
    CreateOutputMsgPage(wpWelcome, 'Command name already in use',
      'Hollywood CLI selected a safe command name',
      'The existing hollywood command will not be replaced.' + #13#10#13#10 +
      'This installation will be available as hollywood-cli.');
end;

function RuntimeFolderForInstall: String;
begin
  Result := SelectedRuntime;
end;

function HasUnownedCommandInDir(Dir, Command: String): Boolean;
var
  BaseName: String;
begin
  BaseName := AddBackslash(Dir) + Command;
  Result := FileExists(BaseName) or FileExists(BaseName + '.exe') or FileExists(BaseName + '.cmd') or
    FileExists(BaseName + '.bat') or FileExists(BaseName + '.com');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  TargetDir: String;
begin
  Result := True;
  if CurPageID = wpSelectDir then begin
    TargetDir := WizardDirValue;
    if (not IsExistingInstallPath(TargetDir)) and HasUnownedCommandInDir(TargetDir, SelectedCommand) then begin
      MsgBox('The selected folder already contains a "' + SelectedCommand + '" command:' +
        Chr(13) + Chr(10) + TargetDir + Chr(13) + Chr(10) + Chr(13) + Chr(10) +
        'Choose another installation folder. The existing file will not be overwritten.',
        mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function ContainsPathEntry(PathValue, Entry: String): Boolean;
var
  Separator: Integer;
  Candidate: String;
begin
  Result := False;
  Entry := NormalizeDir(Entry);
  while PathValue <> '' do begin
    Separator := Pos(';', PathValue);
    if Separator = 0 then begin
      Candidate := PathValue;
      PathValue := '';
    end else begin
      Candidate := Copy(PathValue, 1, Separator - 1);
      Delete(PathValue, 1, Separator);
    end;
    Candidate := UnquotePath(Candidate);
    if (Candidate <> '') and (NormalizeDir(Candidate) = Entry) then begin
      Result := True;
      Exit;
    end;
  end;
end;

procedure NotifyEnvironmentChanged;
var
  MessageResult: Longint;
begin
  SendMessageTimeoutW(HwndBroadcast, WmSettingChange, 0, 'Environment',
    SmtoAbortIfHung, 5000, MessageResult);
end;

function AtomicWriteLauncher(FileName: String; Content: AnsiString): Boolean;
var
  TempName: String;
begin
  TempName := FileName + '.new';
  Result := SaveStringToFile(TempName, Content, False);
  if Result then
    Result := MoveFileExW(TempName, FileName, MoveFileReplaceExisting or MoveFileWriteThrough);
  if (not Result) and FileExists(TempName) then
    DeleteFile(TempName);
end;

function LauncherFileName(Dir: String): String;
begin
  Result := AddBackslash(Dir) + SelectedCommand + '.cmd';
end;

function BashLauncherFileName(Dir: String): String;
begin
  Result := AddBackslash(Dir) + SelectedCommand;
end;

function BuildLauncherContent(Version: String): AnsiString;
var
  Executable: String;
begin
  Executable := '"%~dp0versions\' + Version + '\' + SelectedRuntime + '\hollywood.exe"';
  Result := '@echo off' + #13#10 + 'setlocal' + #13#10 + Executable + ' %*' + #13#10 +
    'exit /b %errorlevel%' + #13#10;
end;

function BuildBashLauncherContent(Version: String): AnsiString;
begin
  Result := '#!/usr/bin/env bash' + #10 +
    'set -e' + #10 +
    'SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"' + #10 +
    'APP="$SCRIPT_DIR/versions/' + Version + '/' + SelectedRuntime + '/hollywood.exe"' + #10 +
    'if [[ ! -f "$APP" ]]; then echo "Hollywood executable not found: $APP" >&2; exit 127; fi' + #10 +
    'if [[ -t 0 && -t 1 && -z "${WT_SESSION:-}" ]] && command -v winpty >/dev/null 2>&1; then' + #10 +
    '  exec winpty "$APP" "$@"' + #10 +
    'fi' + #10 +
    'exec "$APP" "$@"' + #10;
end;

procedure RemoveGeneratedLauncher(FileName: String; IsBash: Boolean);
var
  ActualContent: AnsiString;
  Tail: String;
  Version: String;
  Boundary: Integer;
  Index: Integer;
  Separator: String;
begin
  if not FileExists(FileName) or not LoadStringFromFile(FileName, ActualContent) then
    Exit;
  Separator := '\';
  if IsBash then
    Separator := '/';
  Boundary := Pos('versions' + Separator, String(ActualContent));
  if Boundary = 0 then
    Exit;
  Tail := Copy(String(ActualContent), Boundary + Length('versions') + 1, Length(ActualContent));
  Boundary := Pos(Separator, Tail);
  if (Boundary < 2) or (Boundary > 65) then
    Exit;
  Version := Copy(Tail, 1, Boundary - 1);
  for Index := 1 to Length(Version) do
    if Pos(Version[Index], '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.-') = 0 then
      Exit;
  { The uninstall engine can retain an earlier setup's compiled version.
    Check the complete generated launcher for the version it actually refers to. }
  if IsBash then begin
    if ActualContent = BuildBashLauncherContent(Version) then
      DeleteFile(FileName);
  end else if ActualContent = BuildLauncherContent(Version) then
    DeleteFile(FileName);
end;

function AppExePath(Dir: String): String;
begin
  Result := AddBackslash(Dir) + 'versions\{#AppVersion}\' + SelectedRuntime + '\hollywood.exe';
end;

function VerifyInstalledExecutable(Dir: String): Boolean;
var
  ExitCode: Integer;
  ExecutableSize: Int64;
  Executable: String;
begin
  Executable := AppExePath(Dir);
  Result := FileExists(Executable) and FileSize64(Executable, ExecutableSize) and
    (ExecutableSize > 1048576);
  if Result then
    Result := Exec(Executable, '--version', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and
      (ExitCode = 0);
end;

function SetUserPath(Dir: String; var AddedEntry: Boolean; var PreviousPath: String;
  var PreviousPathExists: Boolean): Boolean;
var
  PathValue: String;
  Existing: Boolean;
begin
  PreviousPathExists := RegQueryStringValue(HKCU, 'Environment', PathValueName, PreviousPath);
  if not PreviousPathExists then
    PreviousPath := '';
  Existing := ContainsPathEntry(PreviousPath, Dir);
  if not Existing and RegQueryStringValue(HKLM,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', PathValueName, PathValue) then
    Existing := ContainsPathEntry(PathValue, Dir);
  if Existing then begin
    AddedEntry := False;
    Result := True;
    Exit;
  end;
  PathValue := PreviousPath;
  if PathValue <> '' then
    PathValue := PathValue + ';';
  PathValue := PathValue + Dir;
  Result := RegWriteExpandStringValue(HKCU, 'Environment', PathValueName, PathValue);
  AddedEntry := Result;
end;

procedure RestoreUserPath(PreviousPath: String; PreviousPathExists: Boolean);
begin
  if PreviousPathExists then
    RegWriteExpandStringValue(HKCU, 'Environment', PathValueName, PreviousPath)
  else
    RegDeleteValue(HKCU, 'Environment', PathValueName);
  NotifyEnvironmentChanged;
end;

procedure RemoveStaleLauncher(Dir, Command: String);
var
  CurrentCommand: String;
begin
  CurrentCommand := SelectedCommand;
  SelectedCommand := Command;
  RemoveGeneratedLauncher(LauncherFileName(Dir), False);
  RemoveGeneratedLauncher(BashLauncherFileName(Dir), True);
  SelectedCommand := CurrentCommand;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppDir: String;
  LauncherName: String;
  BashLauncherName: String;
  LauncherText: AnsiString;
  BashLauncherText: AnsiString;
  PathAdded: Boolean;
  PathExists: Boolean;
  PreviousPath: String;
  PreviousLauncher: AnsiString;
  PreviousLauncherExists: Boolean;
  PreviousBashLauncher: AnsiString;
  PreviousBashLauncherExists: Boolean;
begin
  if CurStep <> ssPostInstall then
    Exit;

  AppDir := ExpandConstant('{app}');
  if not VerifyInstalledExecutable(AppDir) then begin
    DelTree(AddBackslash(AppDir) + 'versions\{#AppVersion}', True, True, True);
    SetInstallFailure('The installed executable failed validation. The previous command registration was preserved.');
    Exit;
  end;

  LauncherName := LauncherFileName(AppDir);
  BashLauncherName := BashLauncherFileName(AppDir);
  PreviousLauncherExists := FileExists(LauncherName);
  PreviousLauncher := '';
  if PreviousLauncherExists then
    LoadStringFromFile(LauncherName, PreviousLauncher);
  PreviousBashLauncherExists := FileExists(BashLauncherName);
  PreviousBashLauncher := '';
  if PreviousBashLauncherExists then
    LoadStringFromFile(BashLauncherName, PreviousBashLauncher);
  LauncherText := BuildLauncherContent('{#AppVersion}');
  BashLauncherText := BuildBashLauncherContent('{#AppVersion}');

  if not AtomicWriteLauncher(LauncherName, LauncherText) then begin
    SetInstallFailure('Hollywood could not create its command launcher. No PATH change was made.');
    Exit;
  end;

  if not AtomicWriteLauncher(BashLauncherName, BashLauncherText) then begin
    if PreviousLauncherExists then
      AtomicWriteLauncher(LauncherName, PreviousLauncher)
    else
      DeleteFile(LauncherName);
    SetInstallFailure('Hollywood could not create its Git Bash launcher. The previous command launcher was restored.');
    Exit;
  end;

  if not SetUserPath(AppDir, PathAdded, PreviousPath, PathExists) then begin
    if PreviousLauncherExists then
      AtomicWriteLauncher(LauncherName, PreviousLauncher)
    else
      DeleteFile(LauncherName);
    if PreviousBashLauncherExists then
      AtomicWriteLauncher(BashLauncherName, PreviousBashLauncher)
    else
      DeleteFile(BashLauncherName);
    SetInstallFailure('Hollywood could not update your user PATH. The previous command launcher was restored.');
    Exit;
  end;

  if not RegWriteStringValue(HKCU, AppInstallStateKey, CommandValueName, SelectedCommand) or
    not RegWriteStringValue(HKCU, AppInstallStateKey, RuntimeValueName, SelectedRuntime) then begin
    if PathAdded then
      RestoreUserPath(PreviousPath, PathExists);
    if PreviousLauncherExists then
      AtomicWriteLauncher(LauncherName, PreviousLauncher)
    else
      DeleteFile(LauncherName);
    if PreviousBashLauncherExists then
      AtomicWriteLauncher(BashLauncherName, PreviousBashLauncher)
    else
      DeleteFile(BashLauncherName);
    SetInstallFailure('Hollywood could not record its installed command. Your previous PATH and launcher were restored.');
    Exit;
  end;

  if PathAdded then begin
    if not RegWriteDWordValue(HKCU, AppInstallStateKey, PathOwnedValueName, 1) then begin
      RestoreUserPath(PreviousPath, PathExists);
      if PreviousLauncherExists then
        AtomicWriteLauncher(LauncherName, PreviousLauncher)
      else
        DeleteFile(LauncherName);
      if PreviousBashLauncherExists then
        AtomicWriteLauncher(BashLauncherName, PreviousBashLauncher)
      else
        DeleteFile(BashLauncherName);
      RegDeleteValue(HKCU, AppInstallStateKey, PathOwnedValueName);
      RegDeleteValue(HKCU, AppInstallStateKey, CommandValueName);
      RegDeleteValue(HKCU, AppInstallStateKey, RuntimeValueName);
      SetInstallFailure('Hollywood could not record its PATH ownership. The previous PATH and command were restored.');
      Exit;
    end;
  end;

  if SelectedCommand = 'hollywood' then
    RemoveStaleLauncher(AppDir, 'hollywood-cli')
  else
    RemoveStaleLauncher(AppDir, 'hollywood');

  NotifyEnvironmentChanged;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then begin
    if InstallFailed then
      WizardForm.FinishedLabel.Caption := 'Hollywood CLI could not be installed.' + #13#10 +
        InstallFailureMessage + #13#10 + 'The existing command and user configuration were preserved.'
    else
      WizardForm.FinishedLabel.Caption := 'Hollywood CLI was installed successfully.' + #13#10 +
        'Open a new terminal and type ' + SelectedCommand + ' to start.';
  end;
end;

procedure RemoveInstalledCommand;
var
  PathValue: String;
  NewPath: String;
  Separator: Integer;
  Entry: String;
  AddedByInstaller: Cardinal;
  AppDir: String;
  StoredCommand: String;
  StoredRuntime: String;
  KeptCount: Integer;
begin
  AppDir := ExpandConstant('{app}');
  if RegQueryStringValue(HKCU, AppInstallStateKey, CommandValueName, StoredCommand) and
    RegQueryStringValue(HKCU, AppInstallStateKey, RuntimeValueName, StoredRuntime) then begin
    SelectedCommand := StoredCommand;
    SelectedRuntime := StoredRuntime;
    RemoveGeneratedLauncher(LauncherFileName(AppDir), False);
    RemoveGeneratedLauncher(BashLauncherFileName(AppDir), True);
  end;

  if RegQueryDWordValue(HKCU, AppInstallStateKey, PathOwnedValueName, AddedByInstaller) and
    (AddedByInstaller = 1) and RegQueryStringValue(HKCU, 'Environment', PathValueName, PathValue) then begin
  NewPath := '';
  KeptCount := 0;
    while PathValue <> '' do begin
      Separator := Pos(';', PathValue);
      if Separator = 0 then begin
        Entry := PathValue;
        PathValue := '';
      end else begin
        Entry := Copy(PathValue, 1, Separator - 1);
        Delete(PathValue, 1, Separator);
      end;
      if NormalizeDir(UnquotePath(Entry)) <> NormalizeDir(AppDir) then begin
        if KeptCount > 0 then
          NewPath := NewPath + ';';
        NewPath := NewPath + Entry;
        KeptCount := KeptCount + 1;
      end;
    end;
    if NewPath = '' then
      RegDeleteValue(HKCU, 'Environment', PathValueName)
    else
      RegWriteExpandStringValue(HKCU, 'Environment', PathValueName, NewPath);
    NotifyEnvironmentChanged;
  end;

  RegDeleteValue(HKCU, AppInstallStateKey, PathOwnedValueName);
  RegDeleteValue(HKCU, AppInstallStateKey, CommandValueName);
  RegDeleteValue(HKCU, AppInstallStateKey, RuntimeValueName);
  RegDeleteKeyIfEmpty(HKCU, AppInstallStateKey);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveInstalledCommand;
end;

function InitializeUninstall: Boolean;
begin
  SelectedCommand := '';
  SelectedRuntime := '';
  Result := True;
end;
