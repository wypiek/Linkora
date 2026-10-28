; The Linkora client installer for Windows, built by build-release.sh with makensis.
;
; One script makes both installers. build-release.sh passes, with /D:
;   VERSION     e.g. 1.1.0
;   OUTFILE     where the installer is written
;   ICON        the application icon
;   CONFIG      the Messenger.config shipped with the client
;   FLAVOR      "windows7"  - the .NET 6 client, for Windows 7 SP1 and later
;               "windows10" - the .NET 8 client, for Windows 10 and 11 only
;   X86DIR, X64DIR, ARM64DIR
;               the published client for each architecture the installer carries
;
; It installs for the current user only, under %LOCALAPPDATA%\Programs, so no administrator
; rights are needed and the client can keep its saved sign in details next to itself.

Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

!define APP "Linkora"
!define EXE "WLMClient.exe"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP}"

; Files the client writes or the user edits; kept when an installation is replaced.
!define KEEP_FILES "Messenger.config|SaveData.xml|Favourites.xml"

Name "${APP}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\${APP}"
BrandingText "${APP} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "${APP} installer"
VIAddVersionKey "LegalCopyright" "${APP}"

!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_ABORTWARNING

!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Start ${APP}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; Which of the carried builds suits this machine: x86, x64 or arm64.
Var Arch

Function .onInit
    !if "${FLAVOR}" == "windows10"
        ${IfNot} ${AtLeastWin10}
            MessageBox MB_OK|MB_ICONSTOP "This installer is for Windows 10 and Windows 11.$\n$\nUse the Windows 7 installer of ${APP} on this computer."
            Abort
        ${EndIf}

        ; The installer itself is 32-bit, so an ARM64 machine also counts as "running x64"; ask
        ; about ARM64 first.
        ${If} ${IsNativeARM64}
            StrCpy $Arch "arm64"
        ${ElseIf} ${RunningX64}
            StrCpy $Arch "x64"
        ${Else}
            MessageBox MB_OK|MB_ICONSTOP "This installer is for 64-bit Windows.$\n$\nUse the Windows 7 installer of ${APP} on 32-bit Windows; it runs on Windows 10 too."
            Abort
        ${EndIf}
    !else
        ${IfNot} ${AtLeastWin7}
            MessageBox MB_OK|MB_ICONSTOP "${APP} needs Windows 7 SP1 or later."
            Abort
        ${EndIf}

        ; No x64 build runs on Windows 10 on ARM, but the 32-bit one does on every ARM PC.
        ${If} ${IsNativeARM64}
            StrCpy $Arch "x86"
        ${ElseIf} ${RunningX64}
            StrCpy $Arch "x64"
        ${Else}
            StrCpy $Arch "x86"
        ${EndIf}

        ; .NET 6 needs SetDefaultDllDirectories, which Windows 7 only gains with update
        ; KB2533623 (superseded by KB3063858). Without it the client closes straight away.
        System::Call 'kernel32::GetModuleHandle(t "kernel32.dll") p .r0'
        System::Call 'kernel32::GetProcAddress(p r0, m "AddDllDirectory") p .r1'
        ${If} $1 = 0
            MessageBox MB_YESNO|MB_ICONEXCLAMATION "This computer is missing Windows update KB3063858, which ${APP} needs. Without it ${APP} will close as soon as it starts.$\n$\nInstall ${APP} anyway, and open the download page for the update?" IDYES openUpdate
            Abort
            openUpdate:
            ${If} ${RunningX64}
                ExecShell "open" "https://www.microsoft.com/download/details.aspx?id=47442"
            ${Else}
                ExecShell "open" "https://www.microsoft.com/download/details.aspx?id=47409"
            ${EndIf}
        ${EndIf}
    !endif
FunctionEnd

; Moves the user's own files aside, clears out an earlier installation so files from another
; architecture cannot linger, and puts the user's files back. Only done where an earlier
; installation of this application is unmistakably present, never to an arbitrary folder.
Function ClearPreviousInstall
    ${IfNot} ${FileExists} "$INSTDIR\${EXE}"
    ${OrIfNot} ${FileExists} "$INSTDIR\Uninstall.exe"
        Return
    ${EndIf}

    CreateDirectory "$PLUGINSDIR\keep"

    Push "${KEEP_FILES}"
    Call MoveKeptFilesOut

    RMDir /r "$INSTDIR"
    CreateDirectory "$INSTDIR"

    CopyFiles /SILENT "$PLUGINSDIR\keep\*.*" "$INSTDIR"
FunctionEnd

Function MoveKeptFilesOut
    Exch $0    ; "a|b|c"
    Push $1
    Push $2

    loop:
        ${If} $0 == ""
            Goto done
        ${EndIf}

        ; Split off the first name.
        StrCpy $1 0
        find:
            StrCpy $2 $0 1 $1
            ${If} $2 == "|"
            ${OrIf} $2 == ""
                Goto found
            ${EndIf}
            IntOp $1 $1 + 1
            Goto find
        found:
            StrCpy $2 $0 $1
            IntOp $1 $1 + 1
            StrCpy $0 $0 "" $1

            ${If} ${FileExists} "$INSTDIR\$2"
                CopyFiles /SILENT "$INSTDIR\$2" "$PLUGINSDIR\keep"
            ${EndIf}

        Goto loop

    done:
    Pop $2
    Pop $1
    Pop $0
FunctionEnd

Section "Install"
    InitPluginsDir

    Call ClearPreviousInstall

    SetOutPath "$INSTDIR"

    !ifdef X86DIR
        ${If} $Arch == "x86"
            File /r /x "Messenger.config" "${X86DIR}/*.*"
        ${EndIf}
    !endif
    !ifdef X64DIR
        ${If} $Arch == "x64"
            File /r /x "Messenger.config" "${X64DIR}/*.*"
        ${EndIf}
    !endif
    !ifdef ARM64DIR
        ${If} $Arch == "arm64"
            File /r /x "Messenger.config" "${ARM64DIR}/*.*"
        ${EndIf}
    !endif

    ; Only a first installation gets the default settings; a user's edited copy is kept.
    SetOverwrite off
    File "/oname=Messenger.config" "${CONFIG}"
    SetOverwrite on

    WriteUninstaller "$INSTDIR\Uninstall.exe"

    SetShellVarContext current
    CreateShortCut "$SMPROGRAMS\${APP}.lnk" "$INSTDIR\${EXE}"
    CreateShortCut "$DESKTOP\${APP}.lnk" "$INSTDIR\${EXE}"

    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${APP}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "${APP}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${EXE}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1

    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" $0
SectionEnd

Section "Uninstall"
    SetShellVarContext current
    Delete "$SMPROGRAMS\${APP}.lnk"
    Delete "$DESKTOP\${APP}.lnk"

    ; Guarded the same way as an upgrade: only a folder this installer made is removed.
    ${If} ${FileExists} "$INSTDIR\${EXE}"
        RMDir /r "$INSTDIR"
    ${Else}
        Delete "$INSTDIR\Uninstall.exe"
        RMDir "$INSTDIR"
    ${EndIf}

    DeleteRegKey HKCU "${UNINSTALL_KEY}"
SectionEnd
