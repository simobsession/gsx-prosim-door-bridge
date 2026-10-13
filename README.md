# GSX ProSim Door Bridge

Standalone door synchronization application for GSX, ProSim B738 and MSFS 2024. No FSUIPC dependency. Version 0.2.4.

Handles GSX requests for all six passenger, service and cargo doors. Synchronizes native L1 movement and includes the L1 aircraft animation fix. Starts automatically through MSFS EXE.xml and runs until MSFS closes. The runtime uses SimConnect and the installed ProSim SDK; PowerShell utilities handle installation and manual start/stop.

Download [version 0.2.4](GSX-ProSim-Door-Bridge-0.2.4.zip), extract to a permanent writable folder outside Community, and close MSFS. Run `Install.ps1 -ProSimDirectory 'D:\ProSimB738'` with your own installation path. See [installation instructions](INSTALLATION.txt) for requirements and configuration. ProSimSDK.dll is loaded from the user's installation and is not distributed.

Version 0.2.4 uses fresh ProSim L1 status and reports closed only when both ProSim and native MSFS report closed. The corrected close sequence passed a live test without a lockup. Full-cycle, actual CDU operation and simulator autostart regression testing remain recommended. The earlier lockup's cause is unconfirmed.

Source: Bridge.cs and Signals.cs. Build as an x64 .NET Framework winexe, referencing your installed ProSimSDK.dll. SimConnect.dll is included for runtime use.
