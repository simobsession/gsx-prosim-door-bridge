GSX ProSim Door Bridge 0.2.4 - MSFS 2024

Compiled external simulator add-on. The runtime needs no PowerShell or FSUIPC.
It connects directly to MSFS through SimConnect and to ProSim through its SDK.
Installer/start/stop utilities use PowerShell. No Community plugin is installed.

Supported aircraft profile: ProSim B738 FS2024 prosim-b738-2026 / 800.
Requirements: Windows x64, .NET Framework 4.8, MSFS 2024, GSX Pro, ProSim B738.
SimConnect.dll is included; the MSFS SDK is not required by users.
ProSimSDK.dll is loaded from your installed ProSim directory and is not bundled.

INSTALL
Close MSFS. Extract to a permanent writable folder outside Community. In PowerShell:
  .\Install.ps1 -ProSimDirectory 'D:\ProSimB738'
The installer can detect the directory if ProSim is running.
The L1 physical animation fix is installed by default in Blended and Scimitar.
The package path is detected from UserCfg.opt, with a standard-folder fallback.
Only the L1 animation expression is patched; other aircraft changes are preserved.
Aircraft model files and layout.json are backed up before editing.
Optional parameters:
  -SimulatorConfigDirectory 'folder containing EXE.xml'
  -AircraftProfileDirectory 'folder containing this aircraft gsx.cfg'
  -ProSimHost 'ProSim PC IP address'
  -AircraftPackageDirectory 'path to Community\prosim-b738'
  -SkipAnimationFix (install only the bridge/profile/startup integration)
For remote ProSim, compatible local SDK files and their dependencies are needed.

Backups are saved under Backups. Existing startup entries are preserved.
One bridge entry is registered in EXE.xml, and exit1/service2 checks are fixed
in the base GSX profile. Existing profile settings are otherwise preserved.
If no base profile exists, the included ProSim 2026 profile is installed.
Airline/tail-specific profiles can override these checks and need review.
Restart Couatl to load changed checks; restart MSFS to confirm autostart.
Keep this folder in place. Reinstall if you move it.

OPERATION
Runs hidden without a time limit; exits when MSFS 2024 closes.
Waits/reconnects if ProSim is unavailable. Only one bridge instance is allowed.
Manual start: Start-Bridge.ps1. Manual stop: Stop-Bridge.ps1.
The STOP file persists; Start-Bridge.ps1 clears it before launching.
Log: bridge-exe.log. Settings: BridgeSettings.xml.

Handles GSX requests for all six passenger, service and cargo doors.
L1 publishes ProSim status to Tony_GSX_ProSim_L1_Open for GSX and synchronizes
native MSFS opening/closing with ProSim using explicit door commands.
A startup guard waits for the published L1 state before interpreting requests.
Doors close when GSX requests closure. Door checks must produce requests.
Commands require on-ground and speed below 1 knot; latching/cooldown prevent
repeated toggles, and intermediate door animation positions are allowed to settle.

UNINSTALL
  .\Install.ps1 -Uninstall
Removes autostart and requests shutdown. GSX profile is retained. Before removing
bridge files, restore the backed-up gsx.cfg or replace exit1's bridge-variable
check, since that check requires the running bridge.

VALIDATION
Prototype live tests covered catering, deboarding and departure. The compiled
SimConnect version has read live GSX and aircraft door states without FSUIPC.
Repeat the full service cycle and one MSFS restart to validate this version.

Source: Bridge.cs and Signals.cs. Build x64 winexe with the Windows .NET Framework
C# compiler, referencing your own installed ProSimSDK.dll.

0.2.4 live validation: native L1 close plus ProSim close cleared the GSX pushback request; both cargo doors subsequently closed and their requests cleared.

L1 ANIMATION FIX
The physical L1 model now reads the greater of its existing B738_door_0 value
and native INTERACTIVE POINT OPEN:0 scaled to the model's animation range.
This preserves ProSim/CDU operation and adds the jetway/native movement path.
Both variants are checked before edits; unexpected animation code aborts the patch.
After an aircraft update, run the installer again with MSFS closed.
To remove only the animation fix, with MSFS closed:
  .\Patch-L1Animation.ps1 -AircraftPackageDirectory 'path to prosim-b738' -RemoveFix
This reverses only the known expression and refreshes layout metadata, preserving
other current aircraft changes. Normal -Uninstall retains this harmless model fix.

0.2.4 validation: Tony confirmed visible L1 opening, visible closing and successful
pushback with no door prompts. Portable patch checks passed for both variants,
removal, repeated installation, and rejecting unexpected code before edits.
0.2.4: L1 ProSim change events synchronize native MSFS door movement so manual ProSim/CDU control is preserved. Direct SDK open/close checks passed; confirm actual CDU behavior during the next live test. Other doors retain their normal CDU path. The bridge commands each GSX request once, not continuously.
0.2.4: L1 status uses a fresh ProSim reading and remains open for GSX while either ProSim or native MSFS reports open. Reduced ProSim subscription and main-loop update rates. Tony confirmed the corrected close sequence worked without a simulator lockup in this test. Full-cycle and autostart regression testing remain recommended; the earlier lockup's cause is unconfirmed.

