using System;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace AeroCtl;

/// <summary>
/// Laptop battery controller
/// </summary>
public class AeroWmiBatteryController(AeroWmi wmi) : IBatteryController
{
	private bool healthSupported = true;
	private bool cyclesSupported = true;

	public BatteryState State
	{
		get
		{
			PInvoke.GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
			if (status.BatteryLifePercent == 255)
				return BatteryState.NoBattery;

			switch (status.ACLineStatus)
			{
				case 1: // Online
					return BatteryState.AC;

				case 0: // Offline
					return BatteryState.DC;
			}

			return BatteryState.NoBattery;
		}
	}

	/// <summary>
	/// Returns the current battery status.
	/// </summary>
	/// <returns></returns>
	public async ValueTask<BatteryStatus> GetStatusAsync()
	{
		return await Task.Run(() =>
		{
			ManagementObject batt, status;
			try
			{
				batt = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Battery")
					.Get()
					.OfType<ManagementObject>()
					.FirstOrDefault();

				status = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM BatteryStatus")
					.Get()
					.OfType<ManagementObject>()
					.FirstOrDefault();
			}
			catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.CallCanceled)
			{
				// Fixes https://gitlab.com/wtwrp/aeroctl/-/issues/70
				return new BatteryStatus();
			}

			int percent = 0;
			double charge = 0;
			double chargeRate = 0;
			double dischargeRate = 0;
			double voltage = 0;

			if (batt?["EstimatedChargeRemaining"] is ushort v)
				percent = v;

			if (status == null)
				return new BatteryStatus();

			if (status.GetPropertyValue("RemainingCapacity") is uint v1 && v1 != uint.MaxValue)
				charge = v1 / 1000.0;

			if (status.GetPropertyValue("ChargeRate") is int v2)
				chargeRate = v2 / 1000.0;

			if (status.GetPropertyValue("DischargeRate") is int v3 && v3 != int.MinValue)
				dischargeRate = v3 / 1000.0;

			if (status.GetPropertyValue("Voltage") is uint v4 && v4 != uint.MaxValue)
				voltage = v4 / 1000.0;

			return new BatteryStatus(charge, percent, chargeRate, dischargeRate, voltage);
		});
	}

	public async ValueTask<ChargePolicy> GetChargePolicyAsync()
	{
		return (ChargePolicy)await wmi.InvokeGetAsync<ushort>("GetChargePolicy");
	}

	public async ValueTask SetChargePolicyAsync(ChargePolicy policy)
	{
		await wmi.InvokeSetAsync("SetChargePolicy", (byte)policy);
	}

	public async ValueTask<int> GetChargeStopAsync()
	{
		return await wmi.InvokeGetAsync<ushort>("GetChargeStop");
	}

	public async ValueTask SetChargeStopAsync(int percent)
	{
		if (percent is <= 0 or > 100)
			throw new ArgumentOutOfRangeException(nameof(percent));

		await wmi.InvokeSetAsync("SetChargeStop", (byte)percent);
	}

	public async ValueTask<bool> GetSmartChargeAsync()
	{
		return await wmi.InvokeGetAsync<byte>("GetSmartCharge") != 0;
	}

	public async ValueTask SetSmargeChargeAsync(bool enabled)
	{
		await wmi.InvokeSetAsync("SetSmartCharge", enabled ? (byte)1 : (byte)0);
	}

	public async ValueTask<int?> GetHealthAsync()
	{
		if (!this.healthSupported)
			return null;

		try
		{
			return await wmi.InvokeGetAsync<byte>("GetBatteryHealth");
		}
		catch (ManagementException)
		{
			this.healthSupported = false;
			return null;
		}
	}

	public async ValueTask<int?> GetCyclesAsync()
	{
		if (!this.cyclesSupported)
			return null;

		try
		{
			int v1 = await wmi.InvokeGetAsync<ushort>("getBattCyc");
			int v2 = await wmi.InvokeGetAsync<ushort>("getBattCyc1");
			return Math.Max(v1, v2);
		}
		catch (ManagementException)
		{
			this.cyclesSupported = false;
			return null;
		}
	}
}