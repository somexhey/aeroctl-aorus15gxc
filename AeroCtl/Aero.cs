using System;
using System.Collections.Immutable;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using ManagedNativeWifi;

namespace AeroCtl;

/// <summary>
/// Implements the AERO interfaces.
/// </summary>
public class Aero : IDisposable
{
	#region Fields

	private AeroWmi wmi;
	private ICpuController cpu;
	private IGpuController gpu;
	private IFanController fans;
	private IKeyboardController keyboard;
	private IBatteryController battery;
	private IDisplayController display;
	private ITouchpadController touchpad;
	private IBluetoothController bluetooth;

	#endregion

	#region Properties

	/// <summary>
	/// Gets the WMI interface.
	/// </summary>
	private AeroWmi Wmi => this.wmi ??= new AeroWmi();

	/// <summary>
	/// Gets the base board / notebook model name.
	/// </summary>
	public string BaseBoard => this.Wmi.BaseBoard;

	/// <summary>
	/// Gets the SKU name of the notebook.
	/// </summary>
	public string Sku => this.Wmi.Sku;

	/// <summary>
	/// Gets the serial number. Should match the one found on the underside of the notebook.
	/// </summary>
	public string SerialNumber => this.Wmi.SerialNumber;

	/// <summary>
	/// Gets the BIOS version strings.
	/// </summary>
	public ImmutableArray<string> BiosVersions => this.Wmi.BiosVersions;

	/// <summary>
	/// Gets the CPU controller.
	/// </summary>
	public ICpuController Cpu => this.cpu ??= new AeroWmiCpuController(this.Wmi);

	/// <summary>
	/// Gets the GPU controller.
	/// </summary>
	public IGpuController Gpu
	{
		get
		{
			if (this.gpu == null)
			{
				if (this.Sku.StartsWith("P7") || this.Sku.StartsWith("P8") || this.Sku.StartsWith("P4") || this.Sku.StartsWith("X5"))
				{
					this.gpu = new P7GpuController(this.Wmi);
				}
				else
				{
					this.gpu = new NvGpuController();
				}
			}

			return this.gpu;
		}
	}

	/// <summary>
	/// Gets Keyboard Fn key handler.
	/// </summary>
	public IKeyboardController Keyboard => this.keyboard ??= new KeyboardController();

	/// <summary>
	/// Gets the fan controller.
	/// </summary>
	public IFanController Fans
	{
		get
		{
			if (this.fans == null)
			{
				if (this.Sku.StartsWith("P7") || this.Sku.StartsWith("P8") || this.Sku.StartsWith("P4") || this.Sku.StartsWith("X5"))
				{
					this.fans = new P7FanController(this.Wmi);
				}
				else
				{
					this.fans = new Aero15Xv8FanController(this.Wmi);
				}
			}

			return this.fans;
		}
	}

	/// <summary>
	/// Gets the screen controller.
	/// </summary>
	public IDisplayController Display => this.display ??= new AeroWmiDisplayController(this.Wmi);

	/// <summary>
	/// Gets the battery stats / controller.
	/// </summary>
	public IBatteryController Battery => this.battery ??= new AeroWmiBatteryController(this.Wmi);

	/// <summary>
	/// Gets the touchpad controller.
	/// </summary>
	public ITouchpadController Touchpad => this.touchpad ??= new RegistryTouchpadController();

	/// <summary>
	/// Gets the Bluetooth controller.
	/// </summary>
	public IBluetoothController Bluetooth => this.bluetooth ??= new AeroWmiBluetoothController(this.Wmi);

	#endregion

	#region Methods

	public ValueTask<bool?> GetWifiEnabledAsync()
	{
		var targetInterface = NativeWifi.EnumerateInterfaces().FirstOrDefault();
		if (targetInterface == null)
			return new ValueTask<bool?>((bool?)null);

		var radioSet = NativeWifi.GetInterfaceRadio(targetInterface.Id)?.RadioSets.FirstOrDefault();
		return new ValueTask<bool?>(radioSet?.SoftwareOn);
	}

	public async ValueTask SetWifiEnabledAsync(bool enabled)
	{
		foreach (var iface in NativeWifi.EnumerateInterfaces())
		{
			var radioSet = NativeWifi.GetInterfaceRadio(iface.Id)?.RadioSets.FirstOrDefault();
			if (radioSet is not { HardwareOn: true })
				continue;

			if (enabled)
				await Task.Run(() => NativeWifi.TurnOnInterfaceRadio(iface.Id));
			else
				await Task.Run(() => NativeWifi.TurnOffInterfaceRadio(iface.Id));
		}
	}

	public async ValueTask<bool> GetCameraEnabledAsync()
	{
		return await this.wmi.InvokeGetAsync<byte>("GetCamera") != 0;
	}

	public async ValueTask SetCameraEnabledAsync(bool enabled)
	{
		await this.wmi.InvokeSetAsync("SetCamera", enabled ? (byte)1 : (byte)0);
	}

	public async ValueTask<bool?> GetSleepUsbCharge()
	{
		try
		{
			return await this.wmi.InvokeGetAsync<byte>("GetSleepUSBCharge") != 0;
		}
		catch (ManagementException) // Not supported on some models.
		{
			return null;
		}
	}

	public async ValueTask SetSleepUsbCharge(bool enabled)
	{
		try
		{
			await this.wmi.InvokeSetAsync("SetSleepUSBCharge", enabled ? (byte)1 : (byte)0);
		}
		catch (ManagementException) // Always thrown by design.
		{
		}
	}

	public async ValueTask<bool?> GetHibernationUsbCharge()
	{
		try
		{
			return await this.wmi.InvokeGetAsync<byte>("GetHibernationUSBCharge") != 0;
		}
		catch (ManagementException) // Not supported on some models.
		{
			return null;
		}
	}

	public async ValueTask SetHibernationUsbCharge(bool enabled)
	{
		try
		{
			await this.wmi.InvokeSetAsync("SetHibernationUSBCharge", enabled ? (byte)1 : (byte)0);
		}
		catch (ManagementException) // Always thrown by design.
		{
		}
	}

	public void Dispose()
	{
		this.Wmi?.Dispose();
		(this.keyboard as IDisposable)?.Dispose();
		(this.touchpad as IDisposable)?.Dispose();
	}

	#endregion
}