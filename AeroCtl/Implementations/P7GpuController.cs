using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;

namespace AeroCtl;

/// <summary>
/// Controller for the newer Aero models that expose additional GPU settings.
/// </summary>
public class P7GpuController : NvGpuController
{
	private readonly AeroWmi wmi;

	[Conditional("DEBUG")]
	private static void Log(string msg)
	{
		File.AppendAllText("aeroctl_debug.log", $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
	}

	public P7GpuController(AeroWmi wmi)
	{
		this.wmi = wmi;
		this.PowerConfigSupported = this.wmi.HasMethod("GetNvPowerConfig");
		this.DynamicBoostSupported = this.wmi.HasMethod("GetDynamicBoostStatus");
		this.AiBoostSupported = this.wmi.HasMethod("GetAIBoostStatus");
		this.ThermalTargetSupported = this.wmi.HasMethod("GetNvThermalTarget");
		this.GpuModeSupported = this.wmi.HasMethod("GetPEG2orSG2");

		Log($"P7GpuController: PowerConfig={this.PowerConfigSupported}, DynamicBoost={this.DynamicBoostSupported}, AiBoost={this.AiBoostSupported}, ThermalTarget={this.ThermalTargetSupported}, GpuMode={this.GpuModeSupported}");
	}

	public override async ValueTask<double?> GetTemperatureAsync()
	{
		// Not sure what the difference between these two is. Query both, just in case.
		try
		{
			return await this.wmi.InvokeGetAsync<ushort>("getGpuTemp1");
		}
		catch (ManagementException)
		{
			try
			{
				return await this.wmi.InvokeGetAsync<ushort>("getGpuTemp2");
			}
			catch (ManagementException)
			{
				return null;
			}
		}
	}

	public bool? PowerConfigSupported { get; private set; }
	private bool powerConfigValue;

	public async Task<bool> GetPowerConfigAsync()
	{
		try
		{
			bool res = await this.invokeGetBoolAsync("GetNvPowerConfig") != false;
			this.PowerConfigSupported = true;
			this.powerConfigValue = res;
			return res;
		}
		catch (ManagementException ex)
		{
			Log($"GetNvPowerConfig failed (setter still works): {ex.Message}");
			return this.powerConfigValue;
		}
	}

	public async Task SetPowerConfigAsync(bool value)
	{
		await this.wmi.InvokeSetAsync<byte>("SetNvPowerConfig", value ? (byte)1 : (byte)0);
		this.powerConfigValue = value;
	}

	public bool? DynamicBoostSupported { get; private set; }
	private bool dynamicBoostValue;

	public async Task<bool> GetDynamicBoostAsync()
	{
		try
		{
			bool res = await this.invokeGetBoolAsync("GetDynamicBoostStatus") != false;
			this.DynamicBoostSupported = true;
			this.dynamicBoostValue = res;
			return res;
		}
		catch (ManagementException ex)
		{
			Log($"GetDynamicBoostStatus failed (setter still works): {ex.Message}");
			return this.dynamicBoostValue;
		}
	}

	public async Task SetDynamicBoostAsync(bool value)
	{
		await this.wmi.InvokeSetAsync<byte>("SetDynamicBoostStatus", value ? (byte)1 : (byte)0);
		this.dynamicBoostValue = value;
	}

	public bool? AiBoostSupported { get; private set; }

	public async Task<bool> GetAiBoostEnabledAsync()
	{
		try
		{
			bool res = await this.invokeGetBoolAsync("GetAIBoostStatus") != false;
			this.AiBoostSupported = true;
			return res;
		}
		catch (ManagementException ex)
		{
			Log($"GetAIBoostStatus failed: {ex.Message}");
			this.AiBoostSupported = false;
			return false;
		}
	}

	public async Task SetAiBoostEnabledAsync(bool value)
	{
		await this.wmi.InvokeSetAsync<byte>("SetAIBoostStatus", value ? (byte)1 : (byte)0);
	}

	public bool? ThermalTargetSupported { get; private set; }

	public async Task<bool> GetThermalTargetEnabledAsync()
	{
		try
		{
			bool res = await this.invokeGetBoolAsync("GetNvThermalTarget") != false;
			this.ThermalTargetSupported = true;
			return res;
		}
		catch (ManagementException ex)
		{
			Log($"GetNvThermalTarget failed: {ex.Message}");
			this.ThermalTargetSupported = false;
			return false;
		}
	}

	public async Task SetThermalTargetEnabledAsync(bool value)
	{
		await this.wmi.InvokeSetAsync<byte>("SetNvThermalTarget", value ? (byte)1 : (byte)0);
	}

	public bool? GpuModeSupported { get; private set; }

	public async Task<int> GetGpuModeAsync()
	{
		try
		{
			byte res = await this.wmi.InvokeGetAsync<byte>("GetPEG2orSG2");
			Log($"GetPEG2orSG2() => {res}");
			this.GpuModeSupported = true;
			return res;
		}
		catch (ManagementException ex)
		{
			Log($"GetPEG2orSG2 failed: {ex.Message}");
			this.GpuModeSupported = false;
			return 0;
		}
	}

	public async Task SetGpuModeAsync(int value)
	{
		await this.wmi.InvokeSetAsync<byte>("SetPEG2orSG2", (byte)value);
	}

	private async Task<bool> invokeGetBoolAsync(string methodName)
	{
		try
		{
			byte result = await this.wmi.InvokeGetAsync<byte>(methodName);
			Log($"{methodName}() => Data={result}");
			return result != 0;
		}
		catch (Exception ex)
		{
			Log($"{methodName} threw: {ex.GetType().Name}: {ex.Message}");
			throw;
		}
	}
}