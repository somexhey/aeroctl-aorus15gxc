# AeroCtl

This is a replacement for the Gigabyte "SmartManager" and/or "ControlCenter" found on the Gigabyte AERO series of notebooks. These apps can not simply be uninstalled without losing some functionality, such as Fn key support (Wifi toggle, display brightness, ...). Since these programs contain a lot of bloat and even require Intel XTU to be running at all times, and are generally pretty bad (how did they even pass QA with typos all over the place?), there was a need to replace them with something minimalist that covers everything not already covered by either standard Windows settings or dedicated tools like ThrottleStop, HWiNFO, etc. It currently implements:

* Querying system information such as Model/SKU strings, BIOS/firmware versions and CPU/GPU temperature.
* Changing display brightness.
* Querying battery information and setting the charging policy / charge stop.
* Fan info and control, including all hardware modes present in the ControlCenter and a fully customizable software fan controller.
* Handling all the non-standard Fn keys such as wifi, touchpad and fan toggle.
* Keyboard RGB LED control, albeit without a fancy UI (see `Samples` folder if you're interested in making a custom effect).
* GPU boost settings on the 2019 AERO (SKU P75*) and newer.

It does not do:
* Overclocking/undervolting. Use Intel XTU or ThrottleStop.
* Managing power plans, just use the Windows UI.
* Whatever that "Azure AI" nonsense is.
* Updating BIOS, keyboard controller firmware or any other driver. You can actually download these yourself (the ControlCenter does nothing else anyway), they contain a standard setup executable that can be run standalone.
* Applying display color management profiles. Again, you can do this yourself by installing the `.icc` files for your model found in the ControlCenter installation directory via the standard Windows color management tool. It even shows them all in a handy dropdown in the usual Windows display settings.
* Fancy UI for customizing the keyboard RGB LEDs. Pull requests welcome. Otherwise just create your own little program, see the Samples directory.

Beware, this tool talks to various APIs, most of them proprietary and undocumented, so use at your own risk. As of now, it has been tested on an AERO 15Xv8 and AERO 15-SA. Their APIs differ in some areas such as fan control and GPU settings, but I doubt the other AERO models will be much different. From what I can tell Aorus is also very similar, but someone will need to verify this.

This program likely will not run on a clean Windows installation as it depends on the Gigabyte ACPI WMI driver. I believe the only thing you need is `C:\Windows\SysWOW64\acpimof.dll` and its respective registry entry (see the "Installation" part [here](https://github.com/microsoft/Windows-driver-samples/tree/master/wmi/wmiacpi#installation)), but I have not tested this. The easiest way is to just install the Gigabyte app and disable all its autostarts and services.

## Installation using the official ControlCenter/SmartManager

1. Install Gigabyte ControlCenter or SmartManager if it isn't already.
2. Install [Microsoft .NET 6.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-6.0.9-windows-x64-installer) if it isn't already.
3. Quit CC/SM and disable its autostart (e.g. via Task Manager's Autostart tab)
4. Reboot
5. Download the latest binary release package (https://gitlab.com/wtwrp/aeroctl/-/releases).
    * Alternatively, a development build can be downloaded from [here](https://f.0x.re/aeroctl/master/AeroCtl.UI.exe)
6. Run `AeroCtl.UI.exe` as administrator from the unpacked directory.
7. (Optional) Use Windows' task scheduler to add a task to autostart AeroCtl as Administrator on logon.

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

## License

[GPLv3](https://gitlab.com/wtwrp/aeroctl/-/blob/master/LICENSE)
