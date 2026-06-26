namespace AeroCtl;

/// <summary>
/// Contains statistics/measurements about a battery.
/// </summary>
/// <param name="Charge">The current battery charge in Wh</param>
/// <param name="ChargePercent">The (estimated) charge percent</param>
/// <param name="ChargeRate">The charge rate in W</param>
/// <param name="DischargeRate">The discharge rate in W</param>
/// <param name="Voltage">The battery voltage in V</param>
public readonly record struct BatteryStatus(double Charge, int ChargePercent, double ChargeRate, double DischargeRate, double Voltage);