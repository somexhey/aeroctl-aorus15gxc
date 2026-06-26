using System;
using System.Threading.Tasks;

namespace AeroCtl;

/// <summary>
/// Implements the fan controller and thermal management of the notebook.
/// </summary>
public class P7FanController(AeroWmi wmi) : IFanController, IFanControllerSync
{
	#region Fields

	private const int minFanSpeed = 0;
	private const int maxFanSpeed = 229;
	private const int fanCurvePointCount = 15;

	#endregion

	#region Constructors

	#endregion

	#region Methods

	private static ushort reverse(ushort val)
	{
		return (ushort)((val << 8) | (val >> 8));
	}

	public async ValueTask<(int fan1, int fan2)> GetRpmAsync()
	{
		int rpm1 = reverse(await wmi.InvokeGetAsync<ushort>("getRpm1"));
		int rpm2 = reverse(await wmi.InvokeGetAsync<ushort>("getRpm2"));

		return (rpm1, rpm2);
	}

	public async ValueTask<double> GetPwmAsync()
	{
		return absToRel(await wmi.InvokeGetAsync<byte>("GetFanPWMStatus"));
	}

	private static int relToAbs(double fanSpeed)
	{
		if (fanSpeed <= 0.0)
			return minFanSpeed;
		if (fanSpeed >= 1.0)
			return maxFanSpeed;

		return (int)(minFanSpeed + fanSpeed * (maxFanSpeed - minFanSpeed));
	}

	private static double absToRel(int fanSpeed)
	{
		return (double)(fanSpeed - minFanSpeed) / (maxFanSpeed - minFanSpeed);
	}

	public async ValueTask SetQuietAsync()
	{
		await wmi.InvokeSetAsync<byte>("SetFixedFanStatus", 0);
		// await this.wmi.InvokeSetAsync<byte>("SetFanSpeed", 0);
		await wmi.InvokeSetAsync<byte>("SetStepFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetAutoFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetNvThermalTarget", 1);
	}

	public async ValueTask SetNormalAsync()
	{
		await wmi.InvokeSetAsync<byte>("SetFixedFanStatus", 0);
		// await this.wmi.InvokeSetAsync<byte>("SetFanSpeed", 0);
		await wmi.InvokeSetAsync<byte>("SetStepFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetAutoFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetNvThermalTarget", 0);
	}

	public async ValueTask SetGamingAsync()
	{
		await wmi.InvokeSetAsync<byte>("SetFixedFanStatus", 0);
		// await this.wmi.InvokeSetAsync<byte>("SetFanSpeed", 0);
		await wmi.InvokeSetAsync<byte>("SetStepFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetAutoFanStatus", 1);
		await wmi.InvokeSetAsync<byte>("SetNvThermalTarget", 0);
	}

	public async ValueTask SetAutoAsync(double fanAdjust = 0.25)
	{
		// await this.wmi.InvokeSetAsync<byte>("SetFanSpeed", 0);
		await wmi.InvokeSetAsync<byte>("SetAutoFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetFixedFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetStepFanStatus", 1);
		//await this.wmi.InvokeSetAsync<byte>("SetNvThermalTarget", 0);
		await wmi.InvokeSetAsync<byte>("SetFanAdjustStatus", (byte)relToAbs(fanAdjust));
	}

	public async ValueTask SetFixedAsync(double fanSpeed = 0.25)
	{
		// await this.wmi.InvokeSetAsync<byte>("SetFanSpeed", 0);
		await wmi.InvokeSetAsync<byte>("SetAutoFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetStepFanStatus", 1);
		//await this.wmi.InvokeSetAsync<byte>("SetNvThermalTarget", 0);
		await wmi.InvokeSetAsync<byte>("SetFixedFanStatus", 1);
		await wmi.InvokeSetAsync<byte>("SetFixedFanSpeed", (byte)relToAbs(fanSpeed));
		await wmi.InvokeSetAsync<byte>("SetGPUFanDuty", (byte)relToAbs(fanSpeed)); // Only available on some models (?)
	}

	public void SetFixed(double fanSpeed = 0.25)
	{
		wmi.InvokeSet<byte>("SetAutoFanStatus", 0);
		wmi.InvokeSet<byte>("SetStepFanStatus", 1);
		wmi.InvokeSet<byte>("SetFixedFanStatus", 1);
		wmi.InvokeSet<byte>("SetFixedFanSpeed", (byte)relToAbs(fanSpeed));
		wmi.InvokeSet<byte>("SetGPUFanDuty", (byte)relToAbs(fanSpeed)); // Only available on some models (?)
	}

	public async ValueTask SetCustomAsync()
	{
		await wmi.InvokeSetAsync<byte>("SetAutoFanStatus", 0);
		await wmi.InvokeSetAsync<byte>("SetFixedFanStatus", 0);
		// await this.wmi.InvokeSetAsync<byte>("SetFanSpeed", 0);
		await wmi.InvokeSetAsync<byte>("SetStepFanStatus", 1);
		//await this.wmi.InvokeSetAsync<byte>("SetNvThermalTarget", 0);
	}

	public IFanCurve GetFanCurve()
	{
		return new Curve(this);
	}

	/// <summary>
	/// Returns the fan point at the specified index.
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	public async ValueTask<FanPoint> GetFanCurvePoint(int index)
	{
		if (index is < 0 or >= fanCurvePointCount)
			throw new ArgumentOutOfRangeException(nameof(index));

		var res = await wmi.InvokeAsync("GetFanIndexValue", ("Index", (byte)index));

		return new FanPoint
		{
			Temperature = (byte)res["Temperture"], // sic
			FanSpeed = absToRel((byte)res["Value"]),
		};
	}

	/// <summary>
	/// Sets the fan point at the specified index.
	/// </summary>
	/// <param name="index"></param>
	/// <param name="point"></param>
	public async ValueTask SetFanCurvePoint(int index, FanPoint point)
	{
		if (index is < 0 or >= fanCurvePointCount)
			throw new ArgumentOutOfRangeException(nameof(index));

		await wmi.InvokeAsync("SetFanIndexValue",
			("Index", (byte)index),
			("Temperture", (byte)point.Temperature), // sic
			("Value", (byte)relToAbs(point.FanSpeed)));
	}

	#endregion

	#region Nested Types

	/// <summary>
	/// Fan curve implementation for <see cref="P7FanController"/>.
	/// </summary>
	private sealed class Curve(P7FanController controller) : IFanCurve
	{
		public int Count => fanCurvePointCount;

		public async ValueTask<FanPoint> GetPointAsync(int index) => await controller.GetFanCurvePoint(index);

		public async ValueTask SetPointAsync(int index, FanPoint value) => await controller.SetFanCurvePoint(index, value);
	}

	#endregion
}