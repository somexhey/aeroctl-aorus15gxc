using System;
using System.Management;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AeroCtl;

/// <summary>
/// Implements <see cref="ITouchpadController"/> using the Windows registry keys for touchpads.
/// </summary>
public class RegistryTouchpadController : ITouchpadController, IDisposable
{
	private readonly RegistryKey key;
	private readonly ManagementEventWatcher keyWatcher;

	private static string escape(string str)
	{
		str = str.Replace("'", "\\'");
		str = str.Replace("\"", "\\\"");
		str = str.Replace("\\", "\\\\");
		return str;
	}

	public RegistryTouchpadController()
	{
		const string path = @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad\Status";
		this.key = Registry.CurrentUser.OpenSubKey(path);

		// Observe changes to the registry value.
		WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
		WqlEventQuery query = new WqlEventQuery($"SELECT * FROM RegistryValueChangeEvent WHERE Hive='HKEY_USERS' AND KeyPath='{escape(currentUser.User.Value)}\\\\{escape(path)}' AND ValueName='Enabled'");
		this.keyWatcher = new ManagementEventWatcher(query);
		this.keyWatcher.EventArrived += (s, e) => { this.onEnabledChanged(); };
		this.keyWatcher.Start();
	}

	public event EventHandler EnabledChanged;

	private void onEnabledChanged()
	{
		this.EnabledChanged?.Invoke(this, EventArgs.Empty);
	}

	public ValueTask<bool> GetEnabledAsync()
	{
		object value = this.key.GetValue("Enabled", 0);
		bool state = false;
		if (value is int i)
			state = i != 0;
		return new ValueTask<bool>(state);
	}

	public void Dispose()
	{
		this.keyWatcher.Stop();
		this.keyWatcher.Dispose();
		this.key?.Dispose();
	}
}