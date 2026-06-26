using System.Threading.Tasks;

namespace AeroCtl;

/// <summary>
/// CPU controller interface.
/// </summary>
public interface ICpuController
{
	/// <summary>
	/// Returns the current CPU temperature.
	/// </summary>
	/// <returns></returns>
	ValueTask<double> GetTemperatureAsync();
}