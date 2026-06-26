using NvAPIWrapper.GPU;
using NvAPIWrapper.Native.Exceptions;
using System.Linq;
using System.Threading.Tasks;

namespace AeroCtl;

public class NvGpuController : IGpuController
{
	private PhysicalGPU gpu;

	public virtual ValueTask<double?> GetTemperatureAsync()
	{
		try
		{
			this.gpu ??= PhysicalGPU.GetPhysicalGPUs().FirstOrDefault();

			double? temp = null;
			if (this.gpu != null)
				temp = this.gpu.ThermalInformation.ThermalSensors.Max(s => s.CurrentTemperature);

			return new(temp);
		}
		catch (NVIDIAApiException)
		{
			return new((double?)null);
		}
	}
}