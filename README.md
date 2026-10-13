# GSX ProSim Door Bridge

Portable background utility for GSX, ProSim B738 and MSFS 2024. Version 0.2.5. No FSUIPC dependency and no Windows app installation required.

## Getting started

1. Download [version 0.2.5](GSX-ProSim-Door-Bridge-0.2.5.zip) and extract the entire ZIP to a permanent writable folder outside Community.
2. Close MSFS and double-click **Setup.exe**. Select your ProSim folder, check the detected MSFS/aircraft/GSX folders and click **Apply configuration**. The portable app folder defaults to the extracted folder.
3. Start MSFS. The bridge starts automatically, appears in the notification area and exits when MSFS closes. You can also double-click **GSX-ProSim-Door-Bridge.exe**; it waits for MSFS if started first.

Right-click the tray icon for connection status, configuration, log access or Exit. Exit stops the current instance; MSFS autostart remains enabled. After changing settings, exit and restart the utility. Keep the folder in place; apply configuration again if you move it.

## Included integration

The corrected GSX profile is included at `Profiles/prosim-b738-2026-gsx.cfg`. Configuration installs it if no profile exists; otherwise it backs up the existing profile and changes the L1 and aft-service checks while preserving other settings. Airline/tail profiles may override the base profile. Restart Couatl to load profile changes.

The optional L1 animation fix patches both Blended and Scimitar aircraft variants and backs up aircraft files. Apply it again after an aircraft update. Setup registers one MSFS EXE.xml autostart entry, preserving other entries. No Windows service, registry app registration or system-wide installation is used. Configuration uses bundled PowerShell scripts internally without opening a console.

Requirements: Windows x64, .NET Framework 4.8, MSFS 2024, GSX Pro and ProSim B738 with aircraft profile `prosim-b738-2026 / 800`. ProSimSDK.dll is loaded from your installed ProSim directory and is not distributed. SimConnect.dll is included. For remote ProSim, enter its host/IP and supply compatible SDK files locally.

Handles all six passenger, service and cargo door requests. L1 status stays open for GSX until both ProSim and native MSFS report closed. Commands require the aircraft on the ground below one knot.

## Validation

The 0.2.4 L1 close sequence passed a live test without a lockup. Version 0.2.5 adds the tray interface and graphical configuration; builds, quoting checks, fixture profile installation and repeat autostart checks passed. Live tray behavior, actual CDU operation, full service cycle and autostart regression tests remain to be completed. The earlier simulator lockup's cause is unconfirmed.

## Advanced / removal

PowerShell utilities remain available for advanced use. With MSFS closed, `Install.ps1 -Uninstall` removes the MSFS startup entry and requests shutdown. The profile and animation patch are retained: restore the backed-up GSX profile or replace its bridge-dependent L1 check before removing the utility. `Patch-L1Animation.ps1 -AircraftPackageDirectory 'path to prosim-b738' -RemoveFix` reverses the known animation patch.

Source: Bridge.cs, Signals.cs and Setup.cs. Build x64 .NET Framework winexe files; reference System.Windows.Forms.dll and System.Drawing.dll, plus your installed ProSimSDK.dll for the bridge.
