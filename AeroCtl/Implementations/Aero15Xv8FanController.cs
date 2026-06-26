using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;

namespace AeroCtl;

public class Aero15Xv8FanController(AeroWmi wmi) : IFanController
{
	public async Task<bool> GetAutoFanStatus()
	{
		return await wmi.InvokeGetAsync<byte>("GetAutoFanStatus") != 0;
	}

	public async Task SetAutoFanStatus(bool value)
	{
		await wmi.InvokeSetAsync<byte>("SetAutoFanStatus", value ? (byte)1 : (byte)0);
	}

	public async Task<bool> GetFanFixedStatus()
	{
		return await wmi.InvokeGetAsync<byte>("GetFanFixedStatus") != 0;
	}

	public async Task SetFanFixedStatus(bool value)
	{
		await wmi.InvokeSetAsync<byte>("SetFixedFanStatus", value ? (byte)1 : (byte)0);
	}

	public async Task SetFixedFanSpeed(byte value)
	{
		await wmi.InvokeSetAsync<byte>("SetFixedFanSpeed", value);
	}

	public async Task<bool> GetFanSpeed()
	{
		return await wmi.InvokeGetAsync<byte>("GetFanSpeed") != 0;
	}

	public async Task SetFanSpeed(bool value)
	{
		try
		{
			await wmi.InvokeSetAsync<byte>("SetFanSpeed", value ? (byte)1 : (byte)0);
		}
		catch (ManagementException) // Always thrown by design.
		{
		}
	}

	public async Task SetCurrentFanStep(byte value)
	{
		await wmi.InvokeSetAsync<byte>("SetCurrentFanStep", value);
	}

	public async Task<bool> GetStepFanStatus()
	{
		return await wmi.InvokeGetAsync<byte>("GetStepFanStatus") != 0;
	}

	public async Task SetStepFanStatus(bool value)
	{
		await wmi.InvokeSetAsync<byte>("SetStepFanStatus", value ? (byte)1 : (byte)0);
	}

	public async Task<bool> GetSmartCoolingStatus()
	{
		return await wmi.InvokeGetAsync<byte>("GetSmartCool") != 0;
	}

	public async Task SetSmartCoolingStatus(bool value)
	{
		try
		{
			await wmi.InvokeSetAsync<byte>("SetSmartCool", value ? (byte)1 : (byte)0);
		}
		catch (ManagementException) // Always thrown by design.
		{
		}
	}

	#region IFanController

	public async ValueTask<(int fan1, int fan2)> GetRpmAsync()
	{
		int rpm1 = 0, rpm2 = 0;
		try
		{
			rpm1 = reverse(await wmi.InvokeGetAsync<ushort>("getRpm1"));
			rpm2 = reverse(await wmi.InvokeGetAsync<ushort>("getRpm2"));
		}
		catch (ManagementException)
		{
		}

		return (rpm1, rpm2);
	}

	public async ValueTask<double> GetPwmAsync()
	{
		return await wmi.InvokeGetAsync<byte>("GetFanPWMStatus") / 229.0;
	}

	public async ValueTask SetQuietAsync()
	{
		await this.SetFanFixedStatus(false);
		await this.SetFanSpeed(false);
		await this.SetStepFanStatus(false);
		await this.SetCurrentFanStep(0);
		await this.SetAutoFanStatus(false);
		await this.SetSmartCoolingStatus(true);
	}

	public async ValueTask SetNormalAsync()
	{
		await this.SetFanFixedStatus(false);
		await this.SetFanSpeed(false);
		await this.SetSmartCoolingStatus(false);
		await this.SetStepFanStatus(false);
		await this.SetCurrentFanStep(0);
		await this.SetAutoFanStatus(false);
	}

	public async ValueTask SetGamingAsync()
	{
		await this.SetFanFixedStatus(false);
		await this.SetFanSpeed(false);
		await this.SetSmartCoolingStatus(false);
		await this.SetStepFanStatus(false);
		await this.SetCurrentFanStep(0);
		await this.SetAutoFanStatus(true);
	}

	public async ValueTask SetFixedAsync(double fanSpeed = 0.25)
	{
		Debug.Assert(fanSpeed is >= 0.0 and <= 1.0);

		await this.SetFanFixedStatus(true);
		await this.SetFanSpeed(false);
		await this.SetSmartCoolingStatus(false);
		await this.SetStepFanStatus(false);
		await this.SetCurrentFanStep(0);
		await this.SetAutoFanStatus(false);
		await this.SetFixedFanSpeed((byte)Math.Round(fanSpeed * 229.0));
	}

	public ValueTask SetAutoAsync(double fanAdjust = 0.25)
	{
		throw new NotSupportedException();
	}

	public ValueTask SetCustomAsync()
	{
		throw new NotSupportedException();
	}

	public IFanCurve GetFanCurve()
	{
		throw new NotSupportedException();
	}

	#endregion

	private static ushort reverse(ushort val)
	{
		return (ushort)((val << 8) | (val >> 8));
	}
}