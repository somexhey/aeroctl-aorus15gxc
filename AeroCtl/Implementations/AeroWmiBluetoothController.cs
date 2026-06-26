using System.Management;
using System.Threading.Tasks;

namespace AeroCtl;

public class AeroWmiBluetoothController(AeroWmi wmi) : IBluetoothController
{
	public async ValueTask<bool> GetEnabledAsync()
	{
		return await wmi.InvokeGetAsync<byte>("GetBluetooth") == 1;
	}

	public async ValueTask SetEnabledAsync(bool enabled)
	{
		await wmi.InvokeSetAsync("SetBluetooth", enabled ? (byte)1 : (byte)0);
		try
		{
			await wmi.InvokeSetAsync("SetBluetoothLED", enabled ? (byte)1 : (byte)0);
		}
		catch (ManagementException) // Always thrown by design.
		{
		}
	}
}