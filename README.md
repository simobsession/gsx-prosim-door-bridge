# GSX ProSim Door Bridge

Portable tray utility for GSX, ProSim B738 and MSFS 2024. Version 0.2.11. No FSUIPC or PowerShell dependency. No Windows service or app installation required.

## Download and configure

1. Download the application ZIP from [the latest release](https://github.com/simobsession/gsx-prosim-door-bridge/releases/latest), rather than GitHub's Source code ZIP.
2. Extract the complete ZIP to a permanent writable folder outside Community. Close MSFS and exit any old bridge instance.
3. Double-click **Setup.exe**. Select your ProSim folder and verify the detected MSFS config, aircraft package and GSX profile folders. Click **Apply configuration**.
4. Restart Couatl to load the GSX profile. Start MSFS; the bridge launches automatically and appears in the notification area.

You can also double-click **GSX-ProSim-Door-Bridge.exe**. It waits for MSFS if started first and exits after the simulator closes. Right-click its tray icon for status, configuration, log access and Exit. Exit stops the current instance without removing autostart. Restart the bridge after changing its settings. Keep its portable folder in place; reapply configuration if it moves.

## Requirements and paths

Windows x64, .NET Framework 4.8, MSFS 2024, GSX Pro and ProSim B738 with aircraft profile `prosim-b738-2026 / 800`. Steam and Microsoft Store/Xbox app editions on Windows are supported. SimConnect.dll is bundled; ProSimSDK.dll is loaded from your installed ProSim folder and is not distributed. Remote ProSim requires compatible SDK files locally and its host/IP in Setup.

Setup checks Steam's roaming folder, Store LocalCache and current-user WpSystem folders on other drives. Verify the chosen config folder if multiple editions are installed. Aircraft detection uses UserCfg.opt; Browse supports custom paths.

The GSX user-profile folder is `%APPDATA%\Virtuali\Airplanes\prosim-b738-2026\800`. It is separate from the Community aircraft package. Setup creates it and installs the included profile when absent; no manual profile creation is needed. Existing profiles are backed up and only the L1 and aft-service checks are corrected. Airline/tail profiles may override the base profile.

## Integration

Handles all six passenger, service and cargo door requests. L1 remains open for GSX until both ProSim and native MSFS report closed. Door commands require the aircraft on the ground below one knot.

The optional L1 animation fix patches both Blended and Scimitar models and refreshes layout metadata. Reapply after aircraft updates. Configuration preserves other MSFS startup entries and creates one bridge autostart entry. File backups copy contents without changing source encryption or permissions.

Configuration and uninstall run directly inside Setup.exe. No PowerShell, encoded commands, execution-policy changes or shell processes are used. This does not guarantee acceptance by every antivirus product.

## SDK diagnostics

Supports public string or parameterless ProSimConnect constructors, provided the remaining required SDK interface is compatible. The bridge logs the loaded DLL path, size, SHA-256, versions and constructors in bridge-exe.log. An incompatibility shows an error and exits after dismissal. Send the log for diagnosis; version labels do not select behavior.

## Uninstall / upgrade

Close MSFS. Open Setup.exe in the configured portable folder and click **Uninstall integration**. It restores recorded original GSX checks, removes an unchanged profile it created, reverses the L1 animation fix, removes autostart and clears runtime settings/logs. Unrelated subsequent changes are preserved; conflicting door-check changes require review. Close Setup and delete the portable folder to remove remaining files and local backups.

Restoration records from 0.2.6–0.2.10 remain supported. For earlier integrations, apply configuration first to record the known legacy checks; unrecorded historical settings cannot be reconstructed exactly. To upgrade, exit the bridge and replace application files in the same folder, preserving BridgeSettings.xml, IntegrationState.json and Backups. Do not overwrite configured settings with the blank template.

## Source

Bridge.cs, Signals.cs and SdkCompatibility.cs build the x64 .NET Framework bridge, referencing your installed ProSimSDK.dll. Setup.cs and Installation.cs build the x64 GUI, referencing System.Web.Extensions.dll. Both builds reference System.Windows.Forms.dll and System.Drawing.dll and embed DoorBridge.ico using `/win32icon:DoorBridge.ico /resource:DoorBridge.ico,DoorBridge.ico`.

Executables and runtime dependencies are distributed only through Releases. The repository contains essential source, the icon, configuration template, GSX profile and this guide.
