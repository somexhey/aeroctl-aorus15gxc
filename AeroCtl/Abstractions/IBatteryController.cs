using System.Threading.Tasks;

namespace AeroCtl;

public interface IBatteryController
{
	BatteryState State { get; }

	ValueTask<BatteryStatus> GetStatusAsync();

	ValueTask<ChargePolicy> GetChargePolicyAsync();
	ValueTask SetChargePolicyAsync(ChargePolicy policy);

	ValueTask<int> GetChargeStopAsync();
	ValueTask SetChargeStopAsync(int percent);

	ValueTask<bool> GetSmartChargeAsync();
	ValueTask SetSmargeChargeAsync(bool enabled);

	ValueTask<int?> GetHealthAsync();
	ValueTask<int?> GetCyclesAsync();
}