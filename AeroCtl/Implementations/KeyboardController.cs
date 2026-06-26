using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.HumanInterfaceDevice;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.UI.Input;
using Microsoft.Win32.SafeHandles;

namespace AeroCtl;

// I don't know whether this is the only 'protocol' Aero laptops use.

/// <summary>
/// Controller for Aero's Fn-key HID device.
/// </summary>
public class KeyboardController : IKeyboardController, IDisposable
{
	#region Fields

	/// <summary>
	/// Keyboard vendor IDs and product IDs for Gigabyte laptop keyboards.
	/// Taken from Gigabyte ControlCenter.
	/// </summary>
	private static readonly IReadOnlyDictionary<ushort, ushort[]> supportedKeyboards = new Dictionary<ushort, ushort[]>()
	{
		{
			0x04D9,
			[
				0x8008
			]
		},
		{
			// This is likely the ITE-829x device.
			0x1044,
			[
				0x7A38, // Japan variant (?)
				0x7A39,
				0x7A3A,
				0x7A3B, // UK/EU variant (?)
				0x7A3C,
				0x7A3D,
				0x7A3C,
				0x7A3D,
				0x7A3E,
				0x7A3F // US variant (?)
			]
		}
	};

	private DummyForm form;
	private readonly HidDevice[] usbDevs;

	#endregion

	#region Properties

	/// <summary>
	/// Gets the RGB LED controller, if present.
	/// </summary>
	public IRgbController Rgb { get; }

	#endregion

	#region Constructors

	public unsafe KeyboardController()
	{
		// Get HID GUID.
		PInvoke.HidD_GetHidGuid(out Guid hidGuid);

		List<HidDevice> devs = [];

		using SetupDiDestroyDeviceInfoListSafeHandle classDevs = PInvoke.SetupDiGetClassDevs(hidGuid, null, HWND.Null, PInvoke.DIGCF_DEVICEINTERFACE | PInvoke.DIGCF_PRESENT);
		try
		{
			const int bufSize = 1024;
			Span<byte> buf = stackalloc byte[4 + bufSize];
			fixed (byte* bufPtr = buf)
			{
				SP_DEVICE_INTERFACE_DETAIL_DATA_W* detailData = (SP_DEVICE_INTERFACE_DETAIL_DATA_W*)bufPtr;
				detailData->cbSize = IntPtr.Size == 8 ? 8U : (uint)(4 + Marshal.SystemDefaultCharSize);

				// Enumerate HID devices.
				for (uint index = 0;; ++index)
				{
					SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
					if (!PInvoke.SetupDiEnumDeviceInterfaces(classDevs, null, hidGuid, index, ref deviceInterfaceData))
						break; // End of list.

					SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
					uint requiredSize = 0;
					if (!PInvoke.SetupDiGetDeviceInterfaceDetail(classDevs, deviceInterfaceData, detailData, bufSize, &requiredSize, &deviceInfoData))
						throw new Win32Exception(Marshal.GetLastWin32Error());

					// Found one.
					// Try to open device.
					string devPath = Marshal.PtrToStringUni(new IntPtr(&detailData->DevicePath._0));
					SafeFileHandle devHandle = PInvoke.CreateFile(
						devPath,
						FILE_ACCESS_FLAGS.FILE_GENERIC_READ | FILE_ACCESS_FLAGS.FILE_GENERIC_WRITE,
						FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
						null,
						FILE_CREATION_DISPOSITION.OPEN_EXISTING,
						FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OVERLAPPED,
						null);

					// Get HID attributes.
					HIDP_CAPS caps;
					if (!PInvoke.HidD_GetAttributes(devHandle, out HIDD_ATTRIBUTES attributes))
					{
						devHandle.Dispose();
						continue;
					}

					// Match against Gigabyte keyboard product IDs.
					if (!supportedKeyboards.TryGetValue(attributes.VendorID, out ushort[] pids) ||
					    !pids.Contains(attributes.ProductID))
					{
						devHandle.Dispose();
						continue;
					}

					// Get HID capabilities.
					IntPtr preparsedData = IntPtr.Zero;
					try
					{
						if (!PInvoke.HidD_GetPreparsedData(devHandle, out preparsedData))
							throw new Win32Exception(Marshal.GetLastWin32Error());

						if (PInvoke.HidP_GetCaps(preparsedData, out caps) != PInvoke.HIDP_STATUS_SUCCESS)
							throw new Win32Exception(Marshal.GetLastWin32Error());
					}
					catch (Win32Exception ex) when (ex.ErrorCode == -2147467259) // Access denied
					{
						Debug.WriteLine($"Access to HID device denied: {devPath}");
						devHandle.Dispose();
						continue;
					}
					finally
					{
						if (preparsedData != IntPtr.Zero)
							PInvoke.HidD_FreePreparsedData(preparsedData);
					}

					// Store device.
					HidDevice dev = new HidDevice(devPath, attributes, caps, devHandle);
					devs.Add(dev);

					// Find RGB controller.
					if (caps is { UsagePage: 0xFF01, Usage: 1, FeatureReportByteLength: 9 })
					{
						this.Rgb = new Ite829XRgbController(dev);
					}
				}
			}

			this.usbDevs = devs.ToArray();
		}
		finally
		{
			if (this.usbDevs == null)
			{
				this.usbDevs = devs.ToArray();
				this.Dispose();
			}
		}
	}

	private event EventHandler<FnKeyEventArgs> fnKeyPressed;

	/// <summary>
	/// Occurs when an Fn key is pressed.
	/// </summary>
	public event EventHandler<FnKeyEventArgs> FnKeyPressed
	{
		add
		{
			this.fnKeyPressed += value;
			this.StartKeyHandling();
		}
		remove => this.fnKeyPressed -= value;
	}

	/// <summary>
	/// Handle raw input message.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	private void onRawInput(object sender, RAWINPUT e)
	{
		if (e.header.dwType == (uint)RID_DEVICE_INFO_TYPE.RIM_TYPEHID && e.data.keyboard.MakeCode == 4)
		{
			switch (e.data.keyboard.Message)
			{
				case 0x7C000004: // Wifi.
					this.fnKeyPressed?.Invoke(this, new FnKeyEventArgs(FnKey.ToggleWifi));
					return;

				case 0x7D000004: // Decrease brightness.
					this.fnKeyPressed?.Invoke(this, new FnKeyEventArgs(FnKey.DecreaseBrightness));
					return;

				case 0x7E000004: // Increase brightness.
					this.fnKeyPressed?.Invoke(this, new FnKeyEventArgs(FnKey.IncreaseBrightness));
					return;

				case 0x80000004: // Screen toggle.
					this.fnKeyPressed?.Invoke(this, new FnKeyEventArgs(FnKey.ToggleScreen));
					return;

				case 0x81000004: // Toggle touchpad on/off.
					this.fnKeyPressed?.Invoke(this, new FnKeyEventArgs(FnKey.ToggleTouchpad));
					return;

				case 0x84000004: // Max fan.
					this.fnKeyPressed?.Invoke(this, new FnKeyEventArgs(FnKey.ToggleFan));
					return;
			}
		}

		Debug.WriteLine($"Unhandled raw input: dwType={e.header.dwType} MakeCode={e.data.keyboard.MakeCode} Flags={e.data.keyboard.Flags:X4} VKey={e.data.keyboard.VKey:X2} Message={e.data.keyboard.Message:X8} Extra={e.data.keyboard.ExtraInformation}");
	}

	#endregion

	#region Methods

	/// <summary>
	/// Starts raw input event handling.
	/// </summary>
	public void StartKeyHandling()
	{
		if (this.form != null)
			return;

		// Create a dummy form to capture the input events.
		this.form = new DummyForm();
		this.form.RawInputReceived += this.onRawInput;
	}

	public void Dispose()
	{
		if (this.form != null)
		{
			if (this.form.InvokeRequired)
				this.form.BeginInvoke(new Action(() => { this.form?.Dispose(); }));
			else
				this.form.Dispose();
		}

		if (this.usbDevs != null)
		{
			foreach (HidDevice dev in this.usbDevs)
			{
				dev.Dispose();
			}
		}
	}

	#endregion

	#region Nested Types

	/// <summary>
	/// Dummy form that receives the raw input events.
	/// </summary>
	private sealed class DummyForm : Form
	{
		public event EventHandler<RAWINPUT> RawInputReceived;

		public DummyForm()
		{
			this.CreateHandle();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			HWND target = (HWND)this.Handle;

			Span<RAWINPUTDEVICE> pRawInputDevice = stackalloc RAWINPUTDEVICE[3];

			// Keyboard
			//pRawInputDevice.Add(new RAWINPUTDEVICE
			//{
			//	usUsagePage = 1,
			//	usUsage = 6,
			//	dwFlags = RAWINPUTDEVICE.RIDEV_INPUTSINK | RAWINPUTDEVICE.RIDEV_DEVNOTIFY,
			//	hwndTarget = this.Handle
			//});

			// Mouse
			//pRawInputDevice.Add(new RAWINPUTDEVICE
			//{
			//	usUsagePage = 1,
			//	usUsage = 2,
			//	dwFlags = RAWINPUTDEVICE.RIDEV_INPUTSINK | RAWINPUTDEVICE.RIDEV_DEVNOTIFY,
			//	hwndTarget = this.Handle
			//});

			pRawInputDevice[0] = (new RAWINPUTDEVICE
			{
				usUsagePage = 0xFF00,
				usUsage = 0xFF00,
				dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK | RAWINPUTDEVICE_FLAGS.RIDEV_DEVNOTIFY,
				hwndTarget = target
			});

			pRawInputDevice[1] = (new RAWINPUTDEVICE
			{
				usUsagePage = 0xFF01,
				usUsage = 0x2209,
				dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK | RAWINPUTDEVICE_FLAGS.RIDEV_DEVNOTIFY,
				hwndTarget = target
			});

			pRawInputDevice[2] = (new RAWINPUTDEVICE
			{
				usUsagePage = 0xFF02,
				usUsage = 1,
				dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK | RAWINPUTDEVICE_FLAGS.RIDEV_DEVNOTIFY,
				hwndTarget = target
			});

			if (!PInvoke.RegisterRawInputDevices(pRawInputDevice, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
				throw new ApplicationException("Failed to register raw input device(s).");
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == 0xFF) // WM_INPUT
			{
				uint sizeOfRawInput = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
				uint size = 0;
				RAWINPUT rawInput = default;

				unsafe
				{
					if (PInvoke.GetRawInputData((HRAWINPUT)m.LParam, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, null, ref size, sizeOfRawInput) == unchecked((uint)-1))
						throw new Win32Exception(Marshal.GetLastWin32Error());

					if (PInvoke.GetRawInputData((HRAWINPUT)m.LParam, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, &rawInput, ref size, sizeOfRawInput) == unchecked((uint)-1))
						throw new Win32Exception(Marshal.GetLastWin32Error());
				}

				this.RawInputReceived?.Invoke(this, rawInput);

				return;
			}

			base.WndProc(ref m);
		}
	}

	#endregion
}