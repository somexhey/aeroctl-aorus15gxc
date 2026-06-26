using System.Threading;
using System.Threading.Tasks;

namespace AeroCtl.UI.SoftwareFan;

/// <summary>
/// Temperature input and fan speed output provider.
/// </summary>
public interface ISoftwareFanProvider
{
	/// <summary>
	/// Gets the input temperature for the fan controller.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask<double> GetTemperatureAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Assigns the fan speed.
	/// </summary>
	/// <param name="speed"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken);
}