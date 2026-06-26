using System;
using System.IO;
using Windows.Win32.Devices.HumanInterfaceDevice;
using Microsoft.Win32.SafeHandles;

namespace AeroCtl;

/// <summary>
/// Represents an opened HID instance.
/// </summary>
public class HidDevice : IDisposable
{
	public string Path { get; }
	public HIDD_ATTRIBUTES Attributes { get; }
	public HIDP_CAPS Caps { get; }
	public SafeFileHandle Handle { get; }
	public FileStream Stream { get; }

	public HidDevice(string path, HIDD_ATTRIBUTES attributes, HIDP_CAPS caps, SafeFileHandle handle)
	{
		this.Path = path;
		this.Attributes = attributes;
		this.Caps = caps;
		this.Handle = handle;

		this.Stream = new FileStream(this.Handle, FileAccess.ReadWrite, 4096, true);
	}

	public void Dispose()
	{
		this.Stream.Dispose();
		this.Handle.Close();
		this.Handle.Dispose();
	}
}