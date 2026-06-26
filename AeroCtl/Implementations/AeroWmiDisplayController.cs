// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace AeroCtl;

/// <summary>
/// Controls the built-in display.
/// </summary>
public class AeroWmiDisplayController(AeroWmi wmi) : IDisplayController
{
	// https://docs.microsoft.com/en-us/windows-hardware/customize/power-settings/display-settings-display-brightness-level
	private static readonly Guid VIDEONORMALLEVEL = new("aded5e82-b909-4619-9949-f5d71dac0bcb");

	public int Brightness
	{
		get
		{
			Guid activePlan = PowerManager.GetActivePlan();
			return (int)PowerManager.GetPlanSetting(activePlan, PInvoke.GUID_VIDEO_SUBGROUP, VIDEONORMALLEVEL, PowerManager.GetCurrentMode());
		}
		set
		{
			if (value is < 0 or > 100)
				throw new ArgumentOutOfRangeException(nameof(value));

			Guid activePlan = PowerManager.GetActivePlan();
			PowerManager.SetPlanSetting(activePlan, PInvoke.GUID_VIDEO_SUBGROUP, VIDEONORMALLEVEL, PowerManager.GetCurrentMode(), (uint)value);
			PowerManager.SetActivePlan(activePlan);
		}
	}

	public async Task<bool> ToggleScreenAsync()
	{
		try
		{
			await wmi.InvokeSetAsync<byte>("SetBrightnessOff", 1);
			return true;
		}
		catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.InvalidObject)
		{
			// Apparently this is expected to throw an exception for whatever reason, even though it does toggle the screen.
		}

		return false;
	}

	public async Task<LidStatus> GetLidStatus()
	{
		byte val = await wmi.InvokeGetAsync<byte>("GetLid1Status");

		if (val == 0)
			return LidStatus.Closed;

		return LidStatus.Open;
	}

	private static IEnumerable<DISPLAY_DEVICEW> enumDisplayDevices()
	{
		for (uint i = 0; ; ++i)
		{
			DISPLAY_DEVICEW dev = default;
			dev.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();

			if (!PInvoke.EnumDisplayDevices(null, i, ref dev, 0))
				break;

			yield return dev;
		}
	}

	public string GetIntegratedDisplayName()
	{
		return enumDisplayDevices()
			.Where(d => (d.StateFlags & PInvoke.DISPLAY_DEVICE_REMOTE) == 0)
			.Where(d => d.DeviceID.ToString().Contains("VEN_8086"))
			.Select(d => d.DeviceName.ToString())
			.MinBy(d => d);
	}


	public uint? GetIntegratedDisplayFrequency()
	{
		string devName = this.GetIntegratedDisplayName();
		if (devName == null)
			return null;

		DEVMODEW current = default;
		current.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
		if (!PInvoke.EnumDisplaySettings(devName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref current))
			return null;

		return current.dmDisplayFrequency;
	}

	public IEnumerable<uint> GetIntegratedDisplayFrequencies()
	{
		string devName = this.GetIntegratedDisplayName();
		if (devName == null)
			yield break;

		DEVMODEW current = default;
		current.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
		if (!PInvoke.EnumDisplaySettings(devName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref current))
			yield break;

		HashSet<uint> returnedHz = [];

		for (int j = 0; ; ++j)
		{
			DEVMODEW mode = default;
			mode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
			if (!PInvoke.EnumDisplaySettings(devName, (ENUM_DISPLAY_SETTINGS_MODE)j, ref mode))
				break;

			if (mode.dmPelsWidth == current.dmPelsWidth && mode.dmPelsHeight == current.dmPelsHeight)
			{
				if (returnedHz.Add(mode.dmDisplayFrequency))
					yield return mode.dmDisplayFrequency;
			}
		}
	}

	public bool SetIntegratedDisplayFrequency(uint newFreq)
	{
		string devName = this.GetIntegratedDisplayName();
		if (devName == null)
			return false;

		DEVMODEW current = default;
		current.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
		if (!PInvoke.EnumDisplaySettings(devName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref current))
			return false;

		current.dmDisplayFrequency = newFreq;
		PInvoke.ChangeDisplaySettings(current, 0);

		return true;
	}
}