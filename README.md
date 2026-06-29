# TL;DR:

AI-assisted slopfork of amazing **Aeroctl** - huge thanks to **wtwrp**

Adapted for Aorus 15G XC for improved compatibility
Minor bugs/inconsistencies are expected (duh)

- Fan controller fix - fan control works for both fans now
- GPU Power Management features adapted
- Working iGPU/dGPU switch
- Tray menu quick controls added
- Autostart (via Task Scheduler) and Minimize on Close options added

# AeroCtl

This is a replacement for the Gigabyte "SmartManager" and/or "ControlCenter" found on the Gigabyte AERO series of notebooks. These apps can not simply be uninstalled without losing some functionality, such as Fn key support (Wifi toggle, display brightness, ...). Since these programs contain a lot of bloat and even require Intel XTU to be running at all times, and are generally pretty bad (how did they even pass QA with typos all over the place?), there was a need to replace them with something minimalist that covers everything not already covered by either standard Windows settings or dedicated tools like ThrottleStop, HWiNFO, etc. It currently implements:

* Querying system information such as Model/SKU strings, BIOS/firmware versions and CPU/GPU temperature.
* Changing display brightness.
* Querying battery information and setting the charging policy / charge stop.
* Fan info and control, including all hardware modes present in the ControlCenter and a fully customizable software fan controller.
* Handling all the non-standard Fn keys such as wifi, touchpad and fan toggle.
* Keyboard RGB LED control, albeit without a fancy UI (see `Samples` folder if you're interested in making a custom effect). Quick presets available from the tray menu.
* GPU boost, power config (+10W TDP), thermal target and AI boost settings (SKU P75\* and newer).
* **GPU mode switch** (iGPU / dGPU) via PEG2/SG2 WMI method — persistent across reboots.
* **Tray context menu** with quick access to fan profiles, GPU mode, GPU Boost, Power Config, Charge Stop presets, and RGB color presets.
* **Auto-start with Windows** via Registry Run key.
* **Auto-restart & crash recovery** — minimize to tray on window close; `RegisterApplicationRestart` ensures the process is restarted by Windows if it crashes or is killed.

It does not do:
* Overclocking/undervolting. Use Intel XTU or ThrottleStop.
* Managing power plans, just use the Windows UI.
* Whatever that "Azure AI" nonsense is.
* Updating BIOS, keyboard controller firmware or any other driver. You can actually download these yourself (the ControlCenter does nothing else anyway), they contain a standard setup executable that can be run standalone.
* Applying display color management profiles. Again, you can do this yourself by installing the `.icc` files for your model found in the ControlCenter installation directory via the standard Windows color management tool. It even shows them all in a handy dropdown in the usual Windows display settings.
* Fancy UI for customizing the keyboard RGB LEDs. Pull requests welcome. Otherwise just create your own little program, see the Samples directory.

Beware, this tool talks to various APIs, most of them proprietary and undocumented, so use at your own risk. As of now, it has been tested on an AERO 15Xv8, AERO 15-SA and **AORUS 15G XC**. Their APIs differ in some areas such as fan control and GPU settings, but I doubt the other AERO models will be much different. From what I can tell Aorus is also very similar.

This program likely will not run on a clean Windows installation as it depends on the Gigabyte ACPI WMI driver. I believe the only thing you need is `C:\Windows\SysWOW64\acpimof.dll` and its respective registry entry (see the "Installation" part [here](https://github.com/microsoft/Windows-driver-samples/tree/master/wmi/wmiacpi#installation)), but I have not tested this. The easiest way is to just install the Gigabyte app and disable all its autostarts and services.

## Installation using the official ControlCenter/SmartManager

1. Install Gigabyte ControlCenter or SmartManager if it isn't already.
2. Install [Microsoft .NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) if it isn't already.
3. Quit CC/SM and disable its autostart (e.g. via Task Manager's Autostart tab)
4. Reboot
5. Download the latest binary release.
6. Run `AeroCtl.UI.exe` as administrator from the unpacked directory.
7. (Optional) Check "Auto-start with Windows" in settings — this adds a Registry Run key. Enable "Auto-restart on exit" to have the app minimize to tray on close and auto-restart on crash via Windows Recovery API.

## Manual Installation

It is also possible to skip installation of the official ControlCenter by doing the minimum steps necessary to get the WMI provider installed.

1. Download the Gigabyte ControlCenter or SmartManager.
2. Extract the setup (e.g. using 7-Zip).
3. Locate `acpimof.dll` and copy it to `C:\Windows\SysWOW64`.
4. Open `regedit` and go to `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WmiAcpi`.
5. Create a string value called `MofImagePath` (if it does not exist already) and set it to `C:\Windows\SysWOW64\acpimof.dll`.
6. Reboot
7. Follow steps 5 to 7 of the other installation method.

## Supported devices

### Fully tested

These laptops have been fully tested by the development team.

* Aero 15-SA
* Aero 15Xv8
* Aorus 15G XC (SKU X5LXC) — dual-fan control via P7FanController, GPU features (Boost, Power Config, Thermal Target, GPU mode switch), tray menu, crash recovery

### Positive feedback

Reports by users having a good experience.

* Aero 15-YA, -YB, -KB, -KD (#27), -XD (#67), -X9 (#40)
* Aero 16-YE5 (#81), -XE4 (#56)
* Aero 17-SA (#71)
* Aorus 15P (#49), -XD (#85)

### Negative feedback

Laptops with open issues or other reports of limited functionality.

* Aorus 15-SA, -X9 (#46)
* Aorus 15G-YB, -KB, -WB (#4)
* Aorus 15 XE5 (#82)
* Aorus 17G-KD (#26)

### Notes on AORUS 15G XC (this fork)

- **SKU X5** is routed to `P7FanController`/`P7GpuController` for proper dual-fan control.
- GPU mode switch uses `PEG2/SG2` (WmiMethodId 230) — persists after reboot. Values: 0 = iGPU, 1 = dGPU. MSHybrid (2) is excluded from the UI as GCC only shows iGPU/dGPU on this model.
- `GetNvPowerConfig` and `GetDynamicBoostStatus` ACPI methods are not implemented in BIOS FB07 — getters return the last cached set value; setters work correctly.
- `BlockWinkey` is present in the WMI schema but the ACPI implementation is missing on FB07.
- Debug logging is written to `aeroctl_debug.log` in the working directory when getter/setter calls fail.

## License

[GPLv3](https://gitlab.com/wtwrp/aeroctl/-/blob/master/LICENSE)
