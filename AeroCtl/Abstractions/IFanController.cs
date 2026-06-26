using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AeroCtl;

/// <summary>
/// Represents a custom fan curve.
/// </summary>
public interface IFanCurve : IAsyncEnumerable<FanPoint>
{
	/// <summary>
	/// Gets the number of points in the fan curve.
	/// </summary>
	int Count { get; }

	/// <summary>
	/// Gets the point at the specified index.
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	ValueTask<FanPoint> GetPointAsync(int index);

	/// <summary>
	/// Sets the point at the specified index.
	/// </summary>
	/// <param name="index"></param>
	/// <param name="value"></param>
	/// <returns></returns>
	ValueTask SetPointAsync(int index, FanPoint value);

	async IAsyncEnumerator<FanPoint> IAsyncEnumerable<FanPoint>.GetAsyncEnumerator(CancellationToken cancellationToken)
	{
		for (int i = 0; i < this.Count; ++i)
		{
			cancellationToken.ThrowIfCancellationRequested();

			yield return await this.GetPointAsync(i);
		}
	}
}

/// <summary>
/// Fan controller interface.
/// </summary>
public interface IFanController
{
	/// <summary>
	/// Returns the current fan RPM values.
	/// </summary>
	/// <returns></returns>
	ValueTask<(int fan1, int fan2)> GetRpmAsync();

	/// <summary>
	/// Returns the current fan PWM value.
	/// </summary>
	/// <returns></returns>
	ValueTask<double> GetPwmAsync();

	/// <summary>
	/// Returns the current hardware custom fan curve.
	/// </summary>
	/// <returns></returns>
	IFanCurve GetFanCurve();

	/// <summary>
	/// Applies the "quiet" profile
	/// </summary>
	/// <returns></returns>
	ValueTask SetQuietAsync();

	/// <summary>
	/// Applies the "normal" profile.
	/// </summary>
	/// <returns></returns>
	ValueTask SetNormalAsync();

	/// <summary>
	/// Applies the "gaming" profile.
	/// </summary>
	/// <returns></returns>
	ValueTask SetGamingAsync();

	/// <summary>
	/// Sets a fixed fan speed.
	/// </summary>
	/// <param name="fanSpeed"></param>
	/// <returns></returns>
	ValueTask SetFixedAsync(double fanSpeed = 0.25);

	/// <summary>
	/// Sets the fan to auto with the specified 'adjust' value.
	/// </summary>
	/// <param name="fanAdjust"></param>
	/// <returns></returns>
	ValueTask SetAutoAsync(double fanAdjust = 0.25);

	/// <summary>
	/// Applies the "custom" profile. Use <see cref="GetFanCurve"/> to change the fan curve.
	/// </summary>
	/// <returns></returns>
	ValueTask SetCustomAsync();
}

/// <summary>
/// Synchronous fan controller interface to set fixed fan speed without yielding.
/// </summary>
public interface IFanControllerSync
{
	void SetFixed(double fanSpeed = 0.25);
}