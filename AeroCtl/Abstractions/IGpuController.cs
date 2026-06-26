using System.Threading.Tasks;

namespace AeroCtl;

/// <summary>
/// GPU controller interface.
/// </summary>
public interface IGpuController
{
	ValueTask<double?> GetTemperatureAsync();
}