using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace AeroCtl.UI;

/// <summary>
/// Implements methods to control the Windows OSD.
/// </summary>
internal static class WindowsOsd
{
	private static HWND getOsdWindow()
	{
		return PInvoke.FindWindow("NativeHWNDHost", "");
	}

	/// <summary>
	/// Tries to get the Windows OSD host window.
	/// </summary>
	public static async ValueTask<HWND> FindOsdWindowAsync()
	{
		if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version.Build >= 22000)
		{
			// Windows 11 does not have this OSD anymore.
			return HWND.Null;
		}

		HWND hWnd = getOsdWindow();
		if (hWnd != IntPtr.Zero)
			return hWnd;

		// The OSD window doesn't exist when it was never used or explorer.exe isn't running,
		// so we emulate mute/unmute keypress here to force it to show up. The OSD be overridden by
		// whatever event follows so it should only be visible for 1 frame or so.

		INPUT[] inputs = new INPUT[2];

		inputs[0].type = INPUT_TYPE.INPUT_KEYBOARD;
		inputs[1].type = INPUT_TYPE.INPUT_KEYBOARD;

		inputs[0].Anonymous.ki.wVk = VIRTUAL_KEY.VK_VOLUME_MUTE;
		inputs[1].Anonymous.ki.wVk = VIRTUAL_KEY.VK_VOLUME_MUTE;

		inputs[1].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

		int cbSize = Marshal.SizeOf(typeof(INPUT));

		for (int i = 0; i < 3; ++i)
		{
			PInvoke.SendInput(inputs, cbSize);
			await Task.Delay(1);

			PInvoke.SendInput(inputs, cbSize);
			await Task.Delay(1);

			hWnd = getOsdWindow();
			if (hWnd != IntPtr.Zero)
				return hWnd;

			await Task.Delay(10);
		}

		// Give up.
		return HWND.Null;
	}

	/// <summary>
	/// Shows the standard Windows brightness slider OSD.
	/// </summary>
	/// <returns></returns>
	public static async ValueTask<bool> ShowBrightnessAsync()
	{
		HWND hWnd = await FindOsdWindowAsync();
		if (hWnd.IsNull)
			return false;

		uint msg = PInvoke.RegisterWindowMessage("SHELLHOOK");
		return PInvoke.PostMessage(hWnd, msg, new WPARAM(0x37), default);
	}
}