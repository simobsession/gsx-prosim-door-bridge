# GSX ProSim Door Bridge

Portable background utility for GSX, ProSim B738 and MSFS 2024. Version 0.2.8. No FSUIPC dependency and no Windows app installation required.

## Getting started

1. Download [latest version](https://github.com/simobsession/gsx-prosim-door-bridge/releases/latest) and extract the entire ZIP to a permanent writable folder outside Community.
2. Close MSFS and double-click **Setup.exe**. Select your ProSim folder, check the detected MSFS/aircraft/GSX folders and click **Apply configuration**. The portable app folder defaults to the extracted folder.
3. Start MSFS. The bridge starts automatically, appears in the notification area and exits when MSFS closes. You can also double-click **GSX-ProSim-Door-Bridge.exe**; it waits for MSFS if started first.

Right-click the tray icon for connection status, configuration, log access or Exit. Exit stops the current instance; MSFS autostart remains enabled. After changing settings, exit and restart the utility. Keep the folder in place; apply configuration again if you move it.

## Included integration

The corrected GSX profile is included at `Profiles/prosim-b738-2026-gsx.cfg`. Configuration installs it if no profile exists; otherwise it backs up the existing profile and changes the L1 and aft-service checks while preserving other settings. Airline/tail profiles may override the base profile. Restart Couatl to load profile changes.

The optional L1 animation fix patches both Blended and Scimitar aircraft variants and backs up aircraft files. Apply it again after an aircraft update. Setup registers one MSFS EXE.xml autostart entry, preserving other entries. No Windows service, registry app registration or system-wide installation is used. Configuration uses bundled PowerShell scripts internally without opening a console.

Requirements: Windows x64, .NET Framework 4.8, MSFS 2024, GSX Pro and ProSim B738 with aircraft profile `prosim-b738-2026 / 800`. ProSimSDK.dll is loaded from your installed ProSim directory and is not distributed. SimConnect.dll is included. For remote ProSim, enter its host/IP and supply compatible SDK files locally.

Handles all six passenger, service and cargo door requests. L1 status stays open for GSX until both ProSim and native MSFS report closed. Commands require the aircraft on the ground below one knot.

## Uninstall

Close MSFS, open Setup.exe and click **Uninstall integration**. The utility restores the recorded original GSX checks (or removes an unchanged profile it created), reverses its L1 animation fix, removes MSFS autostart and clears runtime settings/logs. Then close Setup and delete the portable folder to remove application files and local backups. No Windows service or app registration exists.

Original checks are recorded on first configuration and retained across updates. Uninstall preserves unrelated subsequent profile edits and other startup entries. If a affected check or generated profile was modified, removal stops for review instead of discarding those changes. For installations before 0.2.6, apply configuration with 0.2.7 first: the known legacy bridge L1 check is reverted to the normal B738_door_0 check; unrecorded historical settings cannot be reconstructed exactly.

Source: Bridge.cs, Signals.cs and Setup.cs. Build x64 .NET Framework winexe files, referencing System.Windows.Forms.dll and System.Drawing.dll plus your installed ProSimSDK.dll for the bridge.

Version 0.2.7 adds an embedded multi-resolution door icon to the tray, application executable and configuration window. Rebuilding requires /win32icon:DoorBridge.ico and /resource:DoorBridge.ico,DoorBridge.ico for both executables.




## Microsoft Store and first-time GSX setup

Steam and Microsoft Store/Xbox app editions of MSFS 2024 on Windows are supported. Setup checks the Steam roaming folder, Store LocalCache folder and current-user WpSystem folders on other drives. If both editions are installed, verify the selected config folder. Changing the config folder refreshes aircraft package detection using UserCfg.opt; custom paths can also be selected with Browse.

The GSX profile belongs in %APPDATA%\Virtuali\Airplanes\prosim-b738-2026\800, not inside the aircraft Community package. Setup creates that folder and installs the included profile if it does not exist. Creating a profile manually in GSX first is not required. Restart Couatl after applying configuration. Aircraft/tail-specific profiles may override the base profile.

Version 0.2.8 uses content-based backups for protected Store files and displays concise errors with operation/file context. It does not change source-file encryption or folder permissions.

