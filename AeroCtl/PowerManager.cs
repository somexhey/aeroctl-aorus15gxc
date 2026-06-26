// ReSharper disable InconsistentNaming

using System;
using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace AeroCtl;

[Flags]
public enum PowerMode
{
	AC = 1,
	DC = 2,
}

public static class PowerManager
{
	/// <summary>
	/// Gets the currently active power plan
	/// </summary>
	/// <returns>Guid of the currently active plan</returns>
	public static unsafe Guid GetActivePlan()
	{
		var res = PInvoke.PowerGetActiveScheme(null, out Guid* activePolicyGuid);

		if (res != 0)
			throw new Win32Exception((int)res);

		var guid = activePolicyGuid[0];

		PInvoke.LocalFree((nint)activePolicyGuid);

		return guid;
	}

	/// <summary>
	/// Sets the active power plan
	/// </summary>
	/// <param name="planId">The plan that should be set active.</param>
	public static void SetActivePlan(Guid planId)
	{
		var res = PInvoke.PowerSetActiveScheme(null, planId);

		if (res != 0)
			throw new Win32Exception((int)res);
	}

	/// <summary>
	/// Gets the value for the specified power plan, power mode and setting
	/// </summary>
	/// <param name="plan">Guid of the power plan</param>
	/// <param name="subgroup">The subgroup to look in</param>
	/// <param name="setting">The settign to look up</param>
	/// <param name="powerMode">Power mode. AC or DC, but not both.</param>
	/// <returns>The active index value for the specified setting</returns>
	public static uint GetPlanSetting(Guid plan, Guid subgroup, Guid setting, PowerMode powerMode)
	{
		if (powerMode == (PowerMode.AC | PowerMode.DC))
			throw new ArgumentException("Can't get both AC and DC values at the same time, because they may be different.");

		uint value = 0;
		uint res = 0;

		if (powerMode.HasFlag(PowerMode.AC))
		{
			res = PInvoke.PowerReadACValueIndex(null, plan, subgroup, setting, out value);
		}
		else if (powerMode.HasFlag(PowerMode.DC))
		{
			res = PInvoke.PowerReadDCValueIndex(null, plan, subgroup, setting, out value);
		}

		if (res != 0)
			throw new Win32Exception((int)res);

		return value;
	}

	/// <summary>
	/// Alters a setting on a power plan.
	/// </summary>
	/// <param name="plan">The Guid for the plan you are changing</param>
	/// <param name="subgroup">The Guid for the subgroup the setting belongs to</param>
	/// <param name="setting">The Guid for the setting you are changing</param>
	/// <param name="powerMode">You can chose to alter the AC value, the DC value or both using the bitwise OR operator (|) to join the flags.</param>
	/// <param name="value">The new value for the setting. Run <code>powercfg -q</code> from the command line to list possible values</param>
	public static void SetPlanSetting(Guid plan, Guid subgroup, Guid setting, PowerMode powerMode, uint value)
	{
		if (powerMode.HasFlag(PowerMode.AC))
		{
			var res = PInvoke.PowerWriteACValueIndex(null, plan, subgroup, setting, value);
			if (res != 0)
				throw new Win32Exception((int)res);
		}

		if (powerMode.HasFlag(PowerMode.DC))
		{
			var res = PInvoke.PowerWriteDCValueIndex(null, plan, subgroup, setting, value);
			if (res != 0)
				throw new Win32Exception((int)res);
		}
	}

	/// <summary>
	/// Gets the current AC/DC mode.
	/// </summary>
	/// <returns></returns>
	public static PowerMode GetCurrentMode()
	{
		PInvoke.GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
		return status.ACLineStatus == 1 ? PowerMode.AC : PowerMode.DC;
	}
}