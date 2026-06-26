using System.Threading.Tasks;

namespace AeroCtl;

public interface IBluetoothController
{
	ValueTask<bool> GetEnabledAsync();
	ValueTask SetEnabledAsync(bool enabled);
}