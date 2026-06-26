using System.Threading.Tasks;

namespace AeroCtl;

/// <summary>
/// CPU control exposed through the Aero WMI interface.
/// </summary>
public class AeroWmiCpuController(AeroWmi wmi) : ICpuController
{
	public async ValueTask<double> GetTemperatureAsync()
	{
		return await wmi.InvokeGetAsync<ushort>("getCpuTemp");
	}
}